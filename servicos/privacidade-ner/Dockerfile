FROM python:3.12-slim

WORKDIR /app

# O modelo vem do release oficial do spaCy e fica dentro da imagem: em
# execucao o servico nao acessa a internet.
ARG MODELO_URL=https://github.com/explosion/spacy-models/releases/download/pt_core_news_md-3.8.0/pt_core_news_md-3.8.0-py3-none-any.whl

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt "${MODELO_URL}"

COPY app.py .

RUN useradd --create-home --uid 10001 ner
USER ner

EXPOSE 8000
# Sem log de acesso por requisicao: o caminho e sempre o mesmo e nao ajuda.
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000", "--no-access-log"]
