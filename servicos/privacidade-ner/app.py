"""Reconhecimento de entidades (nomes, lugares, organizacoes) em portugues,
rodando dentro da infraestrutura da clinica.

E a camada estatistica da sanitizacao: o backend manda o texto ja passado
pelas camadas de dicionario e padroes, e este servico aponta os nomes que
nenhuma lista conhece ("o Gervasio, meu cunhado"). Nao decide nada sozinho -
o filtro clinico (epônimos, palavras comuns) fica no backend, junto com as
outras regras da sanitizacao.

O texto da consulta nunca e registrado em log: so o tamanho da requisicao.
"""

import logging
import os

import spacy
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

MODELO = os.environ.get("NER_MODELO", "pt_core_news_md")

# Limite de um trecho de consulta com folga; texto maior que isso nao e fala.
TAMANHO_MAXIMO = 20_000

logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
log = logging.getLogger("privacidade-ner")

# So o reconhecedor de entidades e a classe gramatical sao usados: desligar o
# resto do pipeline deixa a chamada mais rapida.
nlp = spacy.load(MODELO, exclude=["lemmatizer"])

app = FastAPI(title="Prontuario IA - reconhecimento de entidades", docs_url=None, redoc_url=None)


class Pedido(BaseModel):
    texto: str = Field(max_length=TAMANHO_MAXIMO)


class Entidade(BaseModel):
    inicio: int
    fim: int
    rotulo: str
    texto: str
    # Classe gramatical de cada palavra (PROPN, NOUN, VERB...): o backend
    # descarta "Moro em Niteroi" (verbo) mesmo que o modelo o chame de pessoa.
    classes: list[str]
    inicio_de_frase: bool


class Resposta(BaseModel):
    entidades: list[Entidade]


@app.get("/saude")
def saude() -> dict:
    return {"status": "ok", "modelo": MODELO}


@app.post("/entidades", response_model=Resposta)
def entidades(pedido: Pedido) -> Resposta:
    if not pedido.texto.strip():
        return Resposta(entidades=[])

    try:
        documento = nlp(pedido.texto)
    except Exception as erro:  # o backend trata a falha como protecao indisponivel
        log.error("Falha no reconhecimento (%s caracteres): %s", len(pedido.texto), type(erro).__name__)
        raise HTTPException(status_code=500, detail="falha no reconhecimento") from erro

    return Resposta(
        entidades=[
            Entidade(
                inicio=entidade.start_char,
                fim=entidade.end_char,
                rotulo=entidade.label_,
                texto=entidade.text,
                classes=[token.pos_ for token in entidade],
                inicio_de_frase=entidade[0].is_sent_start is True,
            )
            for entidade in documento.ents
        ]
    )
