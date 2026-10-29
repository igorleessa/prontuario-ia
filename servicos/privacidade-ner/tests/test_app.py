"""Contrato do servico com o backend: posicoes, rotulos e classes gramaticais."""

from fastapi.testclient import TestClient

from app import app

cliente = TestClient(app)


def entidades(texto):
    resposta = cliente.post("/entidades", json={"texto": texto})
    assert resposta.status_code == 200
    return resposta.json()["entidades"]


def test_nome_raro_vem_como_pessoa_com_posicao_exata():
    texto = "Quem me trouxe foi o Gervásio, meu cunhado."
    achadas = entidades(texto)

    pessoa = next(e for e in achadas if e["rotulo"] == "PER")
    assert texto[pessoa["inicio"]:pessoa["fim"]] == "Gervásio"
    assert pessoa["classes"] == ["PROPN"]


def test_informa_classe_gramatical_para_o_filtro_do_backend():
    achadas = entidades("Moro em Niterói há dez anos.")

    moro = [e for e in achadas if e["texto"] == "Moro"]
    assert all(e["classes"] != ["PROPN"] for e in moro)


def test_texto_vazio_nao_chama_o_modelo():
    assert entidades("   ") == []


def test_texto_longo_demais_e_recusado():
    resposta = cliente.post("/entidades", json={"texto": "a" * 20_001})
    assert resposta.status_code == 422


def test_saude():
    assert cliente.get("/saude").json()["status"] == "ok"
