#!/usr/bin/env python3
"""Cria pacientes e atendimentos de teste, para a aplicacao nao aparecer vazia
numa demonstracao.

    python3 scripts/dados-demo.py            # rapido, sem custo de IA
    python3 scripts/dados-demo.py --com-ia   # inclui consultas processadas pela IA

Os dados entram pelas mesmas rotas da API que o medico usa - nada de INSERT
direto no banco - entao passam pelas regras de negocio de um atendimento real,
inclusive a trilha de auditoria. Os atendimentos padrao sao confirmados com
conteudo clinico ficticio escrito a mao; a opcao --com-ia usa a rota de consulta
simulada, que chama o provedor de verdade e consome creditos.
"""

import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
ENV = RAIZ / '.env'


def vermelho(texto): print(f'\033[31m{texto}\033[0m')
def verde(texto): print(f'\033[32m{texto}\033[0m')
def cinza(texto): print(f'\033[90m{texto}\033[0m')


def ler_env():
    """Le o .env sem interpretar nada: valores com espaco ou '=' sao literais."""
    if not ENV.exists():
        vermelho('Nao encontrei o .env. Rode ./scripts/setup.sh primeiro.')
        sys.exit(1)

    valores = {}
    for linha in ENV.read_text().splitlines():
        linha = linha.strip()
        if linha and not linha.startswith('#') and '=' in linha:
            chave, valor = linha.split('=', 1)
            valores[chave.strip()] = valor.strip()
    return valores


class Api:
    def __init__(self, base):
        self.base = base
        self.token = None

    def chamar(self, metodo, rota, corpo=None):
        dados = json.dumps(corpo).encode() if corpo is not None else None
        requisicao = urllib.request.Request(f'{self.base}{rota}', data=dados, method=metodo)
        requisicao.add_header('Content-Type', 'application/json')
        if self.token:
            requisicao.add_header('Authorization', f'Bearer {self.token}')

        try:
            with urllib.request.urlopen(requisicao) as resposta:
                conteudo = resposta.read()
                return json.loads(conteudo) if conteudo else None
        except urllib.error.HTTPError as erro:
            detalhe = erro.read().decode(errors='replace')
            raise SystemExit(f'{metodo} {rota} respondeu {erro.code}: {detalhe}')
        except urllib.error.URLError as erro:
            raise SystemExit(f'Nao consegui falar com a API em {self.base}: {erro.reason}')


def template_de(templates, especialidade):
    """Primeiro modelo cuja especialidade casa; None cai no prompt generico."""
    for t in templates:
        if especialidade.lower() in t['especialidade'].lower():
            return t['id']
    return None


# Cada entrada e uma consulta ficticia completa. O conteudo e inventado, mas
# segue a forma de um registro real - e o que se mostra na tela do cliente.
CONSULTAS = [
    {
        'paciente': {
            'nome': 'Helena Souza Ribeiro', 'cpf': '312.845.770-09',
            'dataNascimento': '1968-03-14', 'contato': '(21) 98812-4471', 'idExternoEmr': None,
        },
        'especialidade': 'cardiologia',
        'registro': {
            'queixaPrincipal': 'Dor no peito aos esforcos ha 3 semanas',
            'hda': (
                'Paciente refere dor precordial em aperto, desencadeada por caminhada em aclive, '
                'com alivio ao repouso em cerca de cinco minutos. Nega irradiacao, dispneia '
                'paroxistica noturna ou sincope. Classe funcional II.'
            ),
            'antecedentes': (
                'Hipertensao arterial ha 12 anos, dislipidemia. Pai com infarto aos 58 anos. '
                'Ex-tabagista, cessou ha 6 anos.'
            ),
            'exameFisico': (
                'PA 148x92 mmHg, FC 78 bpm, ritmo regular em dois tempos, sem sopros. Pulsos '
                'perifericos simetricos. Sem edema de membros inferiores.'
            ),
            'hipoteseDiagnostica': 'Angina estavel a esclarecer',
            'cid10Sugerido': 'I20.9',
            'conduta': (
                'Solicitado teste ergometrico e perfil lipidico. Iniciado AAS 100 mg/dia e '
                'atorvastatina 20 mg a noite. Mantida losartana 50 mg. Retorno em 3 semanas.'
            ),
        },
    },
    {
        'paciente': {
            'nome': 'Arthur Lemos Batista', 'cpf': None,
            'dataNascimento': '2021-07-02', 'contato': '(21) 99145-2280', 'idExternoEmr': None,
        },
        'especialidade': 'pediatria',
        'registro': {
            'queixaPrincipal': 'Febre e tosse ha 2 dias, trazido pela mae',
            'hda': (
                'Mae relata febre aferida de 38,4 graus, tosse produtiva e coriza clara desde '
                'anteontem. Aceitacao alimentar reduzida, diurese preservada, sem vomitos ou '
                'diarreia. Irmao mais velho com quadro semelhante na creche.'
            ),
            'antecedentes': (
                'Gestacao e parto sem intercorrencias. Vacinacao em dia. Marcos do desenvolvimento '
                'adequados. Sem alergias conhecidas.'
            ),
            'exameFisico': (
                'Bom estado geral, hidratado, corado. Orofaringe hiperemiada sem placas. Ausculta '
                'pulmonar limpa. Otoscopia sem alteracoes.'
            ),
            'hipoteseDiagnostica': 'Infeccao de vias aereas superiores de provavel etiologia viral',
            'cid10Sugerido': 'J06.9',
            'conduta': (
                'Sintomaticos: paracetamol 15 mg/kg/dose se febre, ate 6/6h. Hidratacao oral '
                'reforcada. Orientados sinais de alarme. Retorno se febre persistir apos 72 horas.'
            ),
        },
    },
    {
        'paciente': {
            'nome': 'Marcos Antunes Villela', 'cpf': '845.221.330-45',
            'dataNascimento': '1985-11-23', 'contato': '(21) 98230-7712', 'idExternoEmr': 'EMR-40128',
        },
        'especialidade': 'ortopedia',
        'registro': {
            'queixaPrincipal': 'Dor no joelho direito apos corrida',
            'hda': (
                'Refere dor em face medial do joelho direito iniciada ha 10 dias, apos aumento de '
                'carga de treino. Piora ao subir escadas, sem episodios de travamento ou falseio. '
                'Vem usando gelo por conta propria, com alivio parcial.'
            ),
            'antecedentes': 'Sem cirurgias previas. Pratica corrida de rua ha 4 anos. Sem comorbidades.',
            'exameFisico': (
                'Marcha sem claudicacao. Dor a palpacao da interlinha medial. Amplitude de movimento '
                'preservada. Testes de gaveta e Lachman negativos. Sem derrame articular.'
            ),
            'hipoteseDiagnostica': 'Sindrome de dor patelofemoral a direita',
            'cid10Sugerido': 'M22.2',
            'conduta': (
                'Suspensao temporaria da corrida por 2 semanas. Fisioterapia com enfase em '
                'fortalecimento de quadriceps e gluteo medio. Analgesia com dipirona se dor. '
                'Retorno em 3 semanas.'
            ),
        },
    },
    {
        'paciente': {
            'nome': 'Rita Carvalho do Amaral', 'cpf': '556.109.882-11',
            'dataNascimento': '1993-05-30', 'contato': '(21) 99677-3410', 'idExternoEmr': 'EMR-40311',
        },
        'especialidade': 'ginecologia',
        'registro': {
            'queixaPrincipal': 'Consulta de rotina e renovacao de contraceptivo',
            'hda': (
                'Assintomatica. Ciclos regulares, fluxo moderado, DUM ha 12 dias. Nega dispareunia, '
                'sangramento intermenstrual ou corrimento. Em uso de anticoncepcional combinado ha '
                '3 anos, sem intercorrencias.'
            ),
            'antecedentes': (
                'G1P1A0, parto normal em 2019. Preventivo ha 2 anos, normal. Sem historia familiar '
                'de cancer de mama ou ovario.'
            ),
            'exameFisico': 'Exame ginecologico sem alteracoes. Mamas sem nodulacoes palpaveis.',
            'hipoteseDiagnostica': 'Consulta de rotina, sem alteracoes',
            'cid10Sugerido': 'Z01.4',
            'conduta': (
                'Solicitado preventivo. Mantido anticoncepcional atual. Orientacoes sobre '
                'autoexame. Retorno anual.'
            ),
        },
    },
    {
        'paciente': {
            'nome': 'Sebastiao Nunes Pereira', 'cpf': '223.887.415-60',
            'dataNascimento': '1951-01-18', 'contato': '(21) 98004-5518', 'idExternoEmr': None,
        },
        'especialidade': '',
        'registro': {
            'queixaPrincipal': 'Retorno para acompanhamento de diabetes',
            'hda': (
                'Paciente em acompanhamento de diabetes tipo 2 ha 9 anos. Refere boa adesao a '
                'metformina, com glicemias capilares de jejum entre 120 e 140 mg/dL. Nega poliuria, '
                'polidipsia ou perda ponderal. Relata dificuldade em manter a dieta nos fins de semana.'
            ),
            'antecedentes': 'Diabetes tipo 2, hipertensao arterial. Sem internacoes no ultimo ano.',
            'exameFisico': (
                'PA 136x84 mmHg. Peso 84 kg. Pes sem lesoes, sensibilidade preservada ao '
                'monofilamento. Pulsos pediosos presentes.'
            ),
            'hipoteseDiagnostica': 'Diabetes mellitus tipo 2 em controle parcial',
            'cid10Sugerido': 'E11.9',
            'conduta': (
                'Mantida metformina 850 mg duas vezes ao dia. Solicitada hemoglobina glicada e '
                'funcao renal. Encaminhado a nutricao. Retorno em 3 meses.'
            ),
        },
    },
]

# Segundo atendimento da primeira paciente, para o historico dela ter mais de
# uma linha na demonstracao.
RETORNO = {
    'queixaPrincipal': 'Retorno com resultado do teste ergometrico',
    'hda': (
        'Retorna assintomatica apos inicio do tratamento. Nega novos episodios de dor precordial. '
        'Teste ergometrico sem sinais de isquemia induzida pelo esforco.'
    ),
    'antecedentes': 'Hipertensao arterial, dislipidemia. Ex-tabagista.',
    'exameFisico': 'PA 128x78 mmHg, FC 70 bpm. Ausculta cardiaca e pulmonar sem alteracoes.',
    'hipoteseDiagnostica': 'Angina estavel controlada clinicamente',
    'cid10Sugerido': 'I20.9',
    'conduta': 'Mantido tratamento atual. LDL em 96 mg/dL, mantida atorvastatina. Retorno em 6 meses.',
}

# Atendimentos parados no meio do caminho: a lista de uma clinica real nunca tem
# so registros concluidos.
EM_ABERTO = [
    {
        'paciente': {
            'nome': 'Juliana Prado Moreira', 'cpf': '701.334.928-77',
            'dataNascimento': '1979-09-08', 'contato': '(21) 99312-0084', 'idExternoEmr': 'EMR-40556',
        },
        'especialidade': 'psiquiatria',
        'ate': 'revisao',
    },
    {
        'paciente': {
            'nome': 'Otavio Bezerra Lins', 'cpf': None,
            'dataNascimento': '2002-12-11', 'contato': None, 'idExternoEmr': 'EMR-40712',
        },
        'especialidade': '',
        'ate': 'abertura',
    },
]


def main():
    com_ia = '--com-ia' in sys.argv
    env = ler_env()

    api = Api(f"http://localhost:{env.get('BACKEND_PORT', '8080')}/api")
    email, senha = env.get('SEED_EMAIL'), env.get('SEED_SENHA')

    cinza(f'API: {api.base}')
    api.chamar('GET', '/health')

    api.token = api.chamar('POST', '/auth/login', {'email': email, 'senha': senha})['token']
    cinza(f'Entrando como {email}\n')

    templates = api.chamar('GET', '/templates')

    def abrir(paciente_id, especialidade):
        pedido = {'pacienteRefId': paciente_id, 'templateNotaId': template_de(templates, especialidade)}
        return api.chamar('POST', '/atendimentos', pedido)['id']

    def confirmar(atendimento_id, registro):
        # Mesmo caminho de um atendimento real: sem consentimento e revisao, os
        # status e a auditoria ficariam inconsistentes.
        api.chamar('POST', f'/atendimentos/{atendimento_id}/consentimento', {})
        api.chamar('POST', f'/atendimentos/{atendimento_id}/revisao', {})
        api.chamar('POST', f'/atendimentos/{atendimento_id}/confirmar', registro)

    primeiro_paciente = None
    pacientes_criados = []

    for consulta in CONSULTAS:
        paciente = api.chamar('POST', '/pacientes', consulta['paciente'])
        pacientes_criados.append(paciente['id'])
        primeiro_paciente = primeiro_paciente or paciente['id']

        confirmar(abrir(paciente['id'], consulta['especialidade']), consulta['registro'])
        rotulo = consulta['especialidade'] or 'clinica geral'
        verde(f"{consulta['paciente']['nome']} — {rotulo}, finalizado")

    confirmar(abrir(primeiro_paciente, 'cardiologia'), RETORNO)
    verde(f"{CONSULTAS[0]['paciente']['nome']} — cardiologia, 2o atendimento")

    for pendente in EM_ABERTO:
        paciente = api.chamar('POST', '/pacientes', pendente['paciente'])
        atendimento_id = abrir(paciente['id'], pendente['especialidade'])

        if pendente['ate'] == 'revisao':
            api.chamar('POST', f'/atendimentos/{atendimento_id}/consentimento', {})
            api.chamar('POST', f'/atendimentos/{atendimento_id}/revisao', {})
            verde(f"{pendente['paciente']['nome']} — em revisao")
        else:
            verde(f"{pendente['paciente']['nome']} — aguardando consentimento")

    if com_ia:
        print()
        cinza('Rodando a IA sobre a consulta de exemplo (consome creditos da OpenAI)…')

        for paciente_id in pacientes_criados[:2]:
            atendimento_id = abrir(paciente_id, '')
            api.chamar('POST', f'/atendimentos/{atendimento_id}/simular', {})
            verde('Consulta simulada enfileirada — o rascunho fica pronto em alguns segundos')

    print()
    verde('Pronto.')
    cinza(f"Abra http://localhost:{env.get('FRONTEND_PORT', '4200')} e entre como {email}")


if __name__ == '__main__':
    main()
