#!/usr/bin/env bash
#
# Setup do ambiente local do Prontuário IA (macOS / Linux).
#
# Pergunta usuário e senha do banco, gera a chave JWT, escreve o .env
# e sobe tudo no Docker (Postgres, MinIO, backend e frontend).
#
# Uso:
#   ./scripts/setup.sh            # interativo
#   ./scripts/setup.sh --recriar  # apaga os volumes e recria o banco do zero

set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$RAIZ/.env"
RECRIAR=false

for arg in "$@"; do
  case "$arg" in
    --recriar) RECRIAR=true ;;
    -h|--help) sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Argumento desconhecido: $arg" >&2; exit 1 ;;
  esac
done

info()  { printf '\033[0;36m%s\033[0m\n' "$*"; }
ok()    { printf '\033[0;32m%s\033[0m\n' "$*"; }
# aviso e erro vão para stderr: as funções de pergunta devolvem o valor pelo
# stdout, então qualquer texto solto ali contaminaria o valor capturado.
aviso() { printf '\033[0;33m%s\033[0m\n' "$*" >&2; }
erro()  { printf '\033[0;31m%s\033[0m\n' "$*" >&2; }

# ---------------------------------------------------------------- prereqs ----
if ! command -v docker >/dev/null 2>&1; then
  erro "Docker não encontrado. Instale o Docker Desktop: https://www.docker.com/products/docker-desktop/"
  exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
  erro "'docker compose' não disponível. Atualize o Docker Desktop (Compose V2)."
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  erro "O Docker não está rodando. Abra o Docker Desktop e execute o script novamente."
  exit 1
fi

ok "Docker disponível."

# ------------------------------------------------------------ .env existente --
if [[ -f "$ENV_FILE" ]]; then
  aviso "Já existe um .env em $ENV_FILE"
  read -r -p "Sobrescrever as configurações? [s/N] " resposta
  if [[ ! "$resposta" =~ ^[sS]$ ]]; then
    info "Mantendo o .env atual. Apenas subindo os containers…"
    PULAR_PERGUNTAS=true
  else
    PULAR_PERGUNTAS=false
  fi
else
  PULAR_PERGUNTAS=false
fi

# ---------------------------------------------------------------- perguntas --
perguntar() {
  local rotulo="$1" padrao="$2" valor
  read -r -p "$rotulo [$padrao]: " valor
  echo "${valor:-$padrao}"
}

# Devolve a senha pelo stdout; tudo que é exibido vai para stderr.
perguntar_senha() {
  local rotulo="$1" minimo="${2:-1}" valor confirmacao
  while true; do
    read -r -s -p "$rotulo: " valor; echo >&2
    if [[ ${#valor} -lt $minimo ]]; then
      aviso "A senha precisa ter ao menos $minimo caractere(s)."
      continue
    fi
    # Estes caracteres quebram o parsing do .env / a interpolação do Compose.
    if [[ "$valor" =~ [\$\#\"\'\`] ]]; then
      aviso "Evite os caracteres \$ # \" ' \` na senha (quebram o .env do Docker Compose)."
      continue
    fi
    read -r -s -p "Confirme a senha: " confirmacao; echo >&2
    if [[ "$valor" != "$confirmacao" ]]; then
      aviso "As senhas não conferem. Tente novamente."
      continue
    fi
    printf '%s' "$valor"
    return
  done
}

gerar_chave() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -base64 48 | tr -d '\n'
  else
    head -c 48 /dev/urandom | base64 | tr -d '\n'
  fi
}

if [[ "$PULAR_PERGUNTAS" == false ]]; then
  echo
  info "=== Banco de dados (PostgreSQL) ==="
  POSTGRES_USER=$(perguntar "Usuário do banco" "prontuario")
  POSTGRES_PASSWORD=$(perguntar_senha "Senha do banco")
  POSTGRES_DB=$(perguntar "Nome do banco" "prontuario")
  POSTGRES_PORT=$(perguntar "Porta do Postgres no host" "5432")

  echo
  info "=== Usuário inicial da aplicação (para login) ==="
  SEED_EMAIL=$(perguntar "E-mail do médico" "medico@local.test")
  SEED_SENHA=$(perguntar_senha "Senha do médico")
  SEED_NOME_MEDICO=$(perguntar "Nome do médico" "Medico de Teste")
  SEED_NOME_CLINICA=$(perguntar "Nome da clínica" "Clinica de Teste")

  echo
  info "=== Modalidade de operação da clínica ==="
  echo "  1) Integrado - prontuário nativo completo, com assinatura"
  echo "  2) Conector  - gera a nota clínica e exporta para um EMR externo"
  modo_escolhido=$(perguntar "Escolha" "1")
  if [[ "$modo_escolhido" == "2" ]]; then
    SEED_MODO_OPERACAO="Conector"
  else
    SEED_MODO_OPERACAO="Integrado"
  fi

  echo
  info "=== Portas da aplicação ==="
  BACKEND_PORT=$(perguntar "Porta da API" "8080")
  FRONTEND_PORT=$(perguntar "Porta do frontend" "4200")

  MINIO_USER=$(perguntar "Usuário do MinIO (storage de áudio)" "prontuario")
  # O MinIO se recusa a iniciar com senha de menos de 8 caracteres.
  MINIO_PASSWORD=$(perguntar_senha "Senha do MinIO (mínimo 8 caracteres)" 8)

  info "Gerando chave JWT aleatória…"
  JWT_KEY=$(gerar_chave)

  umask 077
  cat > "$ENV_FILE" <<EOF
# Gerado por scripts/setup.sh em $(date -u '+%Y-%m-%d %H:%M:%S UTC')
# Arquivo local com segredos - NÃO versionar (já está no .gitignore).

POSTGRES_USER=$POSTGRES_USER
POSTGRES_PASSWORD=$POSTGRES_PASSWORD
POSTGRES_DB=$POSTGRES_DB
POSTGRES_PORT=$POSTGRES_PORT

JWT_KEY=$JWT_KEY
JWT_ISSUER=ProntuarioIA
JWT_AUDIENCE=ProntuarioIA.Clientes
JWT_EXPIRACAO_MINUTOS=60

SEED_HABILITADO=true
SEED_EMAIL=$SEED_EMAIL
SEED_SENHA=$SEED_SENHA
SEED_NOME_MEDICO=$SEED_NOME_MEDICO
SEED_NOME_CLINICA=$SEED_NOME_CLINICA
SEED_MODO_OPERACAO=$SEED_MODO_OPERACAO

MINIO_USER=$MINIO_USER
MINIO_PASSWORD=$MINIO_PASSWORD
MINIO_PORT=9000
MINIO_CONSOLE_PORT=9001

BACKEND_PORT=$BACKEND_PORT
FRONTEND_PORT=$FRONTEND_PORT
EOF
  chmod 600 "$ENV_FILE"
  ok ".env criado em $ENV_FILE (permissão 600)."
fi

# Lê valores do .env sem dar "source": valores com espaço (ex.: "Dra Ana")
# seriam interpretados como comando pelo shell.
ler_env() { grep -E "^$1=" "$ENV_FILE" | head -n1 | cut -d= -f2-; }

BACKEND_PORT=$(ler_env BACKEND_PORT)
FRONTEND_PORT=$(ler_env FRONTEND_PORT)
MINIO_CONSOLE_PORT=$(ler_env MINIO_CONSOLE_PORT)
SEED_EMAIL=$(ler_env SEED_EMAIL)
SEED_MODO_OPERACAO=$(ler_env SEED_MODO_OPERACAO)

# ----------------------------------------------------------------- subir ------
cd "$RAIZ"

if [[ "$RECRIAR" == true ]]; then
  aviso "--recriar: removendo containers e volumes (os dados do banco serão perdidos)…"
  docker compose down -v
fi

info "Construindo as imagens e subindo os containers…"
docker compose up -d --build

# ------------------------------------------------------------- healthcheck ----
info "Aguardando a API responder…"
for _ in $(seq 1 60); do
  if curl -fsS "http://localhost:${BACKEND_PORT}/api/health" >/dev/null 2>&1; then
    API_OK=true
    break
  fi
  sleep 2
done

echo
if [[ "${API_OK:-false}" == true ]]; then
  ok "=== Ambiente pronto ==="
  echo "  Frontend:       http://localhost:${FRONTEND_PORT}"
  echo "  API (Swagger):  http://localhost:${BACKEND_PORT}/swagger"
  echo "  MinIO console:  http://localhost:${MINIO_CONSOLE_PORT}"
  echo
  echo "  Login:          ${SEED_EMAIL}"
  echo "  Modalidade:     ${SEED_MODO_OPERACAO}"
  echo
  echo "  Logs:           docker compose logs -f"
  echo "  Parar:          docker compose down"
  echo "  Recriar banco:  ./scripts/setup.sh --recriar"
else
  erro "A API não respondeu em http://localhost:${BACKEND_PORT}/api/health"
  erro "Veja os logs com: docker compose logs backend"
  exit 1
fi
