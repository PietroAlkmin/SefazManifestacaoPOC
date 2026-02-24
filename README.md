# SEFAZ Manifestação POC

**Prova de Conceito** para manifestação de **Ciência da Operação (210210)** de ResNFe da SEFAZ.

## 🎯 Objetivo

Testar o envio de manifestação de ciência sobre um ResNFe específico e validar a resposta da SEFAZ.

## 📋 Dados de Teste

**ResNFe para teste:**
```xml
<?xml version="1.0" encoding="UTF-8" ?>
<resNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
    <chNFe>31260242831263000192550010001096711000000052</chNFe>
    <CNPJ>42831263000192</CNPJ>
    <xNome>MULTIAGUAS DISTRIBUIDORA DE BEBIDAS LTDA</xNome>
    <IE>0628135330021</IE>
    <dhEmi>2026-02-18T00:00:00-03:00</dhEmi>
    <tpNF>1</tpNF>
    <vNF>150.00</vNF>
    <digVal>tWD7C0E6shf05SKjXibkSA8t7p8=</digVal>
    <dhRecbto>2026-02-18T16:13:43-03:00</dhRecbto>
    <nProt>131267314471826</nProt>
    <cSitNFe>1</cSitNFe>
</resNFe>
```

**Dados extraídos:**
- **chNFe**: `31260242831263000192550010001096711000000052`
- **CNPJ Emissor**: `42831263000192`
- **Valor**: R$ 150,00

## 🚀 Como Usar

### 1. Rodar o projeto

```bash
cd "C:\Users\pietr\OneDrive\Área de Trabalho\SefazManifestacaoPOC\SefazManifestacaoPOC"
dotnet run
```

A API estará disponível em: **http://localhost:5000**

O Swagger abrirá automaticamente na raiz.

### 2. Endpoint de Teste

**POST** `/api/sefaz/manifestar-ciencia`

**Body (JSON):**
```json
{
  "chaveNFe": "31260242831263000192550010001096711000000052",
  "cnpjDestinatario": "00000000000000",
  "certificadoPath": "C:\\caminho\\para\\certificado.pfx",
  "certificadoSenha": "senha_do_certificado"
}
```

⚠️ **Importante:**
- Substitua `cnpjDestinatario` pelo CNPJ de quem RECEBEU o ResNFe (14 dígitos, somente números)
- Forneça o caminho completo do certificado digital (.pfx)
- A POC está configurada para **ambiente de HOMOLOGAÇÃO**

### 3. Resposta Esperada (Sucesso)

```json
{
  "success": true,
  "cStat": "135",
  "xMotivo": "Evento registrado e vinculado a NF-e",
  "protocolo": "891260000123456",
  "dataRegistro": "2026-02-24T12:30:00",
  "xmlRetorno": "<?xml...",
  "erro": null
}
```

**cStat:**
- `135` = Evento registrado com sucesso ✅
- `136` = Evento já registrado anteriormente ℹ️
- `573` = Rejeição (duplicidade) ❌
- Outros = Consultar manual da SEFAZ

## 📂 Estrutura do Projeto

```
SefazManifestacaoPOC/
├── Controllers/
│   └── SefazController.cs          # Endpoint principal
├── Models/
│   ├── ManifestacaoRequest.cs      # Request DTO
│   └── ManifestacaoResponse.cs     # Response DTO
├── Services/
│   ├── XmlEventoBuilder.cs         # Constrói XML do evento
│   ├── XmlSignatureService.cs      # Assina digitalmente
│   └── SefazEventoClient.cs        # Cliente SOAP HTTP
└── Program.cs                       # Configuração da aplicação
```

## 🔐 Fluxo de Execução

1. **Recebe Request** → Valida chNFe e CNPJ
2. **Carrega Certificado** → Lê arquivo .pfx com senha
3. **Constrói XML** → Cria estrutura do evento 210210
4. **Assina XML** → Assinatura digital no elemento infEvento
5. **Envia para SEFAZ** → POST SOAP para endpoint de homologação
6. **Processa Resposta** → Extrai cStat, xMotivo, protocolo
7. **Retorna JSON** → Response estruturado

## 🧪 Teste via cURL

```bash
curl -X POST http://localhost:5000/api/sefaz/manifestar-ciencia \
  -H "Content-Type: application/json" \
  -d '{
    "chaveNFe": "31260242831263000192550010001096711000000052",
    "cnpjDestinatario": "12345678000100",
    "certificadoPath": "C:\\certificado.pfx",
    "certificadoSenha": "123456"
  }'
```

## 📖 Referências

- [Manual de Manifestação do Destinatário - SEFAZ](https://www.nfe.fazenda.gov.br/)
- Código do Evento: **210210** (Ciência da Operação)
- Versão do layout: **1.00**
- Ambiente: **Homologação (tpAmb=2)**

## ⚠️ Notas Importantes

- Esta é uma POC simplificada sem persistência de dados
- Configurada APENAS para homologação
- Valida certificados com `ServerCertificateCustomValidationCallback`
- Logs completos no console para debugging
- XML assinado e resposta SEFAZ ficam visíveis nos logs

## 🔧 Próximos Passos (Produção)

- [ ] Adicionar appsettings com configurações
- [ ] Implementar conexão com banco de dados
- [ ] Criar job background para consulta automática
- [ ] Adicionar suporte para outros tipos de evento (210200, 210220, 210240)
- [ ] Implementar NFeDistribuicaoDFe (consulta de ResNFe)
- [ ] Tratar múltiplas UFs com endpoints específicos
- [ ] Adicionar autenticação/autorização na API
