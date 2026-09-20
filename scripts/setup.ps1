<#
.SYNOPSIS
    Setup do ambiente local do Prontuário IA (Windows).

.DESCRIPTION
    Pergunta usuário e senha do banco, gera a chave JWT, escreve o .env
    e sobe tudo no Docker (Postgres, MinIO, backend e frontend).

.PARAMETER Recriar
    Apaga os volumes e recria o banco do zero.

.EXAMPLE
    .\scripts\setup.ps1

.EXAMPLE
    .\scripts\setup.ps1 -Recriar
#>
[CmdletBinding()]
param(
    [switch]$Recriar
)

$ErrorActionPreference = 'Stop'

$Raiz = Split-Path -Parent $PSScriptRoot
$EnvFile = Join-Path $Raiz '.env'

function Write-Info   { param([string]$Texto) Write-Host $Texto -ForegroundColor Cyan }
function Write-Ok     { param([string]$Texto) Write-Host $Texto -ForegroundColor Green }
function Write-Aviso  { param([string]$Texto) Write-Host $Texto -ForegroundColor Yellow }
function Write-Erro   { param([string]$Texto) Write-Host $Texto -ForegroundColor Red }

# ---------------------------------------------------------------- prereqs ----
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Erro 'Docker nao encontrado. Instale o Docker Desktop: https://www.docker.com/products/docker-desktop/'
    exit 1
}

docker compose version *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Erro "'docker compose' nao disponivel. Atualize o Docker Desktop (Compose V2)."
    exit 1
}

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Erro 'O Docker nao esta rodando. Abra o Docker Desktop e execute o script novamente.'
    exit 1
}

Write-Ok 'Docker disponivel.'

# ---------------------------------------------------------------- funcoes ----
function Read-ComPadrao {
    param([string]$Rotulo, [string]$Padrao)

    $valor = Read-Host "$Rotulo [$Padrao]"
    if ([string]::IsNullOrWhiteSpace($valor)) { return $Padrao }
    return $valor
}

function Read-Senha {
    param([string]$Rotulo, [int]$Minimo = 1)

    while ($true) {
        $primeira = Read-Host $Rotulo -AsSecureString
        $texto = [System.Net.NetworkCredential]::new('', $primeira).Password

        if ($texto.Length -lt $Minimo) {
            Write-Aviso "A senha precisa ter ao menos $Minimo caractere(s)."
            continue
        }

        # Estes caracteres quebram o parsing do .env / a interpolacao do Compose.
        if ($texto -match '[\$#"''`]') {
            Write-Aviso 'Evite os caracteres $ # " '' ` na senha (quebram o .env do Docker Compose).'
            continue
        }

        $segunda = Read-Host 'Confirme a senha' -AsSecureString
        $textoConfirmacao = [System.Net.NetworkCredential]::new('', $segunda).Password

        if ($texto -ne $textoConfirmacao) {
            Write-Aviso 'As senhas nao conferem. Tente novamente.'
            continue
        }

        return $texto
    }
}

function New-ChaveAleatoria {
    # RandomNumberGenerator::Create() funciona tanto no Windows PowerShell 5.1
    # (.NET Framework) quanto no PowerShell 7+; o metodo estatico Fill() nao.
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $bytes = New-Object byte[] 48
        $rng.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes)
    } finally {
        $rng.Dispose()
    }
}

# ------------------------------------------------------------ .env existente --
$PularPerguntas = $false
if (Test-Path $EnvFile) {
    Write-Aviso "Ja existe um .env em $EnvFile"
    $resposta = Read-Host 'Sobrescrever as configuracoes? [s/N]'
    if ($resposta -notmatch '^[sS]$') {
        Write-Info 'Mantendo o .env atual. Apenas subindo os containers...'
        $PularPerguntas = $true
    }
}

# ---------------------------------------------------------------- perguntas --
if (-not $PularPerguntas) {
    Write-Host ''
    Write-Info '=== Banco de dados (PostgreSQL) ==='
    $PostgresUser = Read-ComPadrao 'Usuario do banco' 'prontuario'
    $PostgresPassword = Read-Senha 'Senha do banco'
    $PostgresDb = Read-ComPadrao 'Nome do banco' 'prontuario'
    $PostgresPort = Read-ComPadrao 'Porta do Postgres no host' '5432'

    Write-Host ''
    Write-Info '=== Usuario inicial da aplicacao (para login) ==='
    $SeedEmail = Read-ComPadrao 'E-mail do medico' 'medico@local.test'
    $SeedEmailAdmin = Read-ComPadrao 'E-mail do administrador (configura a clinica)' 'admin@local.test'
    $SeedSenha = Read-Senha 'Senha do medico'
    $SeedNomeMedico = Read-ComPadrao 'Nome do medico' 'Medico de Teste'
    $SeedNomeClinica = Read-ComPadrao 'Nome da clinica' 'Clinica de Teste'

    Write-Host ''
    Write-Info '=== Modalidade de operacao da clinica ==='
    Write-Host '  1) Integrado - prontuario nativo completo, com assinatura'
    Write-Host '  2) Conector  - gera a nota clinica e exporta para um EMR externo'
    $modoEscolhido = Read-ComPadrao 'Escolha' '1'
    $SeedModoOperacao = if ($modoEscolhido -eq '2') { 'Conector' } else { 'Integrado' }

    Write-Host ''
    Write-Info '=== Portas da aplicacao ==='
    $BackendPort = Read-ComPadrao 'Porta da API' '8080'
    $FrontendPort = Read-ComPadrao 'Porta do frontend' '4200'

    $MinioUser = Read-ComPadrao 'Usuario do MinIO (storage de audio)' 'prontuario'
    # O MinIO se recusa a iniciar com senha de menos de 8 caracteres.
    $MinioPassword = Read-Senha 'Senha do MinIO (minimo 8 caracteres)' 8

    Write-Info 'Gerando chave JWT aleatoria...'
    $JwtKey = New-ChaveAleatoria

    # Segredo do webhook usado pelo EMR de demonstracao para conferir a assinatura.
    $EmrDemoSecret = (New-ChaveAleatoria) -replace '[^A-Za-z0-9]', ''

    $agora = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')
    $conteudo = @"
# Gerado por scripts/setup.ps1 em $agora UTC
# Arquivo local com segredos - NAO versionar (ja esta no .gitignore).

POSTGRES_USER=$PostgresUser
POSTGRES_PASSWORD=$PostgresPassword
POSTGRES_DB=$PostgresDb
POSTGRES_PORT=$PostgresPort

JWT_KEY=$JwtKey
JWT_ISSUER=ProntuarioIA
JWT_AUDIENCE=ProntuarioIA.Clientes
JWT_EXPIRACAO_MINUTOS=60

SEED_HABILITADO=true
SEED_EMAIL=$SeedEmail
# As telas de configuracao exigem o papel de administrador; o login de medico
# nao mexe em credenciais nem no destino de exportacao da clinica.
SEED_EMAIL_ADMIN=$SeedEmailAdmin
SEED_NOME_ADMIN=Administrador da Clinica
SEED_SENHA=$SeedSenha
SEED_NOME_MEDICO=$SeedNomeMedico
SEED_NOME_CLINICA=$SeedNomeClinica
SEED_MODO_OPERACAO=$SeedModoOperacao

MINIO_USER=$MinioUser
MINIO_PASSWORD=$MinioPassword
MINIO_PORT=9000
MINIO_CONSOLE_PORT=9001

# Dias que o audio bruto fica guardado depois de transcrito (LGPD).
AUDIO_RETENCAO_DIAS=30

# Botao "Simular consulta" na tela do atendimento (demonstracao ao cliente).
DEMONSTRACAO_HABILITADA=true

# Linha de base de documentacao manual usada no calculo de tempo economizado.
MINUTOS_DOCUMENTACAO_MANUAL=7

BACKEND_PORT=$BackendPort
FRONTEND_PORT=$FrontendPort

# EMR ficticio da demonstracao: recebe a nota pelo webhook e a exibe como se
# fosse o prontuario do cliente. O segredo e o mesmo que se cadastra na tela
# de Configuracoes > Exportacao para o EMR.
EMR_DEMO_PORT=9080
EMR_DEMO_SECRET=$EmrDemoSecret
"@

    # UTF8 sem BOM: o Docker Compose nao interpreta BOM no .env.
    [System.IO.File]::WriteAllText($EnvFile, $conteudo, [System.Text.UTF8Encoding]::new($false))
    Write-Ok ".env criado em $EnvFile"
}

# Carrega o .env para uso no proprio script (portas, e-mail etc.).
$config = @{}
Get-Content $EnvFile | ForEach-Object {
    if ($_ -match '^\s*([A-Z_]+)=(.*)$') { $config[$Matches[1]] = $Matches[2] }
}

# ----------------------------------------------------------------- subir ------
Push-Location $Raiz
try {
    if ($Recriar) {
        Write-Aviso '-Recriar: removendo containers e volumes (os dados do banco serao perdidos)...'
        docker compose down -v
    }

    Write-Info 'Construindo as imagens e subindo os containers...'
    docker compose up -d --build
    if ($LASTEXITCODE -ne 0) {
        Write-Erro 'Falha ao subir os containers.'
        exit 1
    }

    # ------------------------------------------------------- healthcheck ------
    Write-Info 'Aguardando a API responder...'
    $backendPort = $config['BACKEND_PORT']
    $apiOk = $false

    foreach ($tentativa in 1..60) {
        try {
            Invoke-RestMethod -Uri "http://localhost:$backendPort/api/health" -TimeoutSec 3 | Out-Null
            $apiOk = $true
            break
        } catch {
            Start-Sleep -Seconds 2
        }
    }

    Write-Host ''
    if ($apiOk) {
        Write-Ok '=== Ambiente pronto ==='
        Write-Host "  Frontend:       http://localhost:$($config['FRONTEND_PORT'])"
        Write-Host "  API (Swagger):  http://localhost:$backendPort/swagger"
        Write-Host "  MinIO console:  http://localhost:$($config['MINIO_CONSOLE_PORT'])"
        Write-Host "  EMR de demo:    http://localhost:$($config['EMR_DEMO_PORT'])"
        Write-Host ""
        Write-Host "  Para demonstrar a modalidade Conector, cadastre em Configuracoes:"
        Write-Host "    Webhook:      http://emr-demo:8080/webhook"
        Write-Host "    Segredo:      $($config['EMR_DEMO_SECRET'])"
        Write-Host ''
        Write-Host "  Login medico:   $($config['SEED_EMAIL'])"
        Write-Host "  Login admin:    $($config['SEED_EMAIL_ADMIN']) (mesma senha; configura a clinica)"
        Write-Host "  Modalidade:     $($config['SEED_MODO_OPERACAO'])"
        Write-Host ''
        Write-Host '  Logs:           docker compose logs -f'
        Write-Host '  Parar:          docker compose down'
        Write-Host '  Recriar banco:  .\scripts\setup.ps1 -Recriar'
    } else {
        Write-Erro "A API nao respondeu em http://localhost:$backendPort/api/health"
        Write-Erro 'Veja os logs com: docker compose logs backend'
        exit 1
    }
} finally {
    Pop-Location
}
