using Microsoft.AspNetCore.Mvc;
using SefazManifestacaoPOC.Models;
using SefazManifestacaoPOC.Services;
using System.Xml.Linq;

namespace SefazManifestacaoPOC.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SefazController : ControllerBase
{
    private readonly XmlEventoBuilder _xmlBuilder;
    private readonly XmlSignatureService _signatureService;
    private readonly SefazEventoClient _eventoClient; // Manual SOAP (antigo)
    private readonly SefazEventoClientWcf _eventoClientWcf; // WCF (novo)
    private readonly ILogger<SefazController> _logger;

    public SefazController(
        XmlEventoBuilder xmlBuilder,
        XmlSignatureService signatureService,
        SefazEventoClient eventoClient,
        SefazEventoClientWcf eventoClientWcf,
        ILogger<SefazController> logger)
    {
        _xmlBuilder = xmlBuilder;
        _signatureService = signatureService;
        _eventoClient = eventoClient;
        _eventoClientWcf = eventoClientWcf;
        _logger = logger;
    }

    /// <summary>
    /// Manifesta ciência da operação (210210) sobre um ResNFe recebido
    /// </summary>
    /// <param name="request">Dados para manifestação</param>
    /// <returns>Resultado da manifestação com resposta da SEFAZ</returns>
    [HttpPost("manifestar-ciencia")]
    [ProducesResponseType(typeof(ManifestacaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManifestacaoResponse>> ManifestarCiencia([FromBody] ManifestacaoRequest request)
    {
        try
        {
            _logger.LogInformation("Iniciando manifestação de ciência para chNFe: {ChaveNFe}", request.ChaveNFe);

            // 1. Validar entrada
            if (string.IsNullOrWhiteSpace(request.ChaveNFe) || request.ChaveNFe.Length != 44)
            {
                return BadRequest(new ManifestacaoResponse
                {
                    Success = false,
                    Erro = "ChaveNFe deve ter 44 dígitos"
                });
            }

            if (string.IsNullOrWhiteSpace(request.CnpjDestinatario) || request.CnpjDestinatario.Length != 14)
            {
                return BadRequest(new ManifestacaoResponse
                {
                    Success = false,
                    Erro = "CNPJ deve ter 14 dígitos (somente números)"
                });
            }

            // 2. Carregar certificado
            _logger.LogInformation("Carregando certificado digital...");
            var certificado = _signatureService.CarregarCertificado(request.CertificadoPath, request.CertificadoSenha);

            // 3. Construir XML do evento
            _logger.LogInformation("Construindo XML do evento de ciência...");
            var xmlEvento = _xmlBuilder.ConstruirEventoCiencia(
                request.ChaveNFe,
                request.CnpjDestinatario,
                isProducao: false // Homologação
            );

            _logger.LogInformation("XML construído: {Xml}", xmlEvento.ToString());

            // 4. Assinar XML
            _logger.LogInformation("Assinando XML digitalmente...");
            var idEvento = $"ID210210{request.ChaveNFe}01";
            var xmlAssinado = _signatureService.AssinarXml(xmlEvento, certificado, idEvento);

            _logger.LogInformation("XML assinado com sucesso");
            _logger.LogInformation("XML ASSINADO COMPLETO: {XmlAssinado}", xmlAssinado.ToString());

            // 5. Enviar para SEFAZ
            _logger.LogInformation("Enviando para SEFAZ...");
            // Extrair UF da chave para enviar ao endpoint correto
            var uf = XmlEventoBuilder.ExtrairUF(request.ChaveNFe);
            _logger.LogInformation("UF extraída da chave: {UF}", uf);
            var xmlResposta = await _eventoClient.EnviarEventoAsync(xmlAssinado, certificado, uf);

            _logger.LogInformation("Resposta recebida da SEFAZ: {Resposta}", xmlResposta);

            // 6. Processar resposta
            var response = ProcessarRespostaSefaz(xmlResposta);

            if (response.Success)
            {
                _logger.LogInformation("Manifestação registrada com sucesso! Protocolo: {Protocolo}", response.Protocolo);
            }
            else
            {
                _logger.LogWarning("Manifestação rejeitada. cStat: {CStat}, Motivo: {Motivo}", response.CStat, response.XMotivo);
            }

            return Ok(response);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "Arquivo de certificado não encontrado");
            return NotFound(new ManifestacaoResponse
            {
                Success = false,
                Erro = $"Certificado não encontrado: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao manifestar ciência");
            return StatusCode(500, new ManifestacaoResponse
            {
                Success = false,
                Erro = $"Erro interno: {ex.Message}"
            });
        }
    }

    private ManifestacaoResponse ProcessarRespostaSefaz(string xmlResposta)
    {
        // Verificar se recebeu HTML 404 ao invés de XML
        if (xmlResposta.Contains("404") || xmlResposta.Contains("Not Found") || xmlResposta.Contains("<html"))
        {
            return new ManifestacaoResponse
            {
                Success = false,
                Erro = "WebService não encontrado (404). Verifique URL no SefazEventoClient.UrlsHomologacao",
                XmlRetorno = xmlResposta
            };
        }

        try
        {
            var doc = XDocument.Parse(xmlResposta);
            XNamespace ns = "http://www.portalfiscal.inf.br/nfe";

            var retEvento = doc.Descendants(ns + "retEvento").FirstOrDefault();
            if (retEvento == null)
            {
                return new ManifestacaoResponse
                {
                    Success = false,
                    XmlRetorno = xmlResposta,
                    Erro = "Formato de resposta inválido"
                };
            }

            var infEvento = retEvento.Element(ns + "infEvento");
            var cStat = infEvento?.Element(ns + "cStat")?.Value;
            var xMotivo = infEvento?.Element(ns + "xMotivo")?.Value;
            var nProt = infEvento?.Element(ns + "nProt")?.Value;
            var dhRegEvento = infEvento?.Element(ns + "dhRegEvento")?.Value;

            var success = cStat == "135" || cStat == "136"; // 135=Registrado, 136=Já registrado

            return new ManifestacaoResponse
            {
                Success = success,
                CStat = cStat,
                XMotivo = xMotivo,
                Protocolo = nProt,
                DataRegistro = DateTime.TryParse(dhRegEvento, out var data) ? data : null,
                XmlRetorno = xmlResposta
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar resposta da SEFAZ");
            return new ManifestacaoResponse
            {
                Success = false,
                XmlRetorno = xmlResposta,
                Erro = $"Erro ao processar resposta: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Health check do serviço
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            message = "SEFAZ Manifestação POC está rodando"
        });
    }

    /// <summary>
    /// Manifesta ciência usando WCF (System.ServiceModel) - NOVA IMPLEMENTAÇÃO
    /// </summary>
    /// <param name="request">Dados para manifestação</param>
    /// <returns>Resultado da manifestação com resposta da SEFAZ</returns>
    [HttpPost("manifestar-ciencia-wcf")]
    [ProducesResponseType(typeof(ManifestacaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ManifestacaoResponse>> ManifestarCienciaWcf([FromBody] ManifestacaoRequest request)
    {
        try
        {
            _logger.LogInformation("[WCF] Iniciando manifestação de ciência para chNFe: {ChaveNFe}", request.ChaveNFe);

            // 1. Validar entrada
            if (string.IsNullOrWhiteSpace(request.ChaveNFe) || request.ChaveNFe.Length != 44)
            {
                return BadRequest(new ManifestacaoResponse
                {
                    Success = false,
                    Erro = "ChaveNFe deve ter 44 dígitos"
                });
            }

            if (string.IsNullOrWhiteSpace(request.CnpjDestinatario) || request.CnpjDestinatario.Length != 14)
            {
                return BadRequest(new ManifestacaoResponse
                {
                    Success = false,
                    Erro = "CNPJ deve ter 14 dígitos (somente números)"
                });
            }

            // 2. Carregar certificado
            _logger.LogInformation("[WCF] Carregando certificado digital...");
            var certificado = _signatureService.CarregarCertificado(request.CertificadoPath, request.CertificadoSenha);

            // 3. Construir XML do evento
            _logger.LogInformation("[WCF] Construindo XML do evento de ciência...");
            var xmlEvento = _xmlBuilder.ConstruirEventoCiencia(
                request.ChaveNFe,
                request.CnpjDestinatario,
                isProducao: false // Homologação
            );

            _logger.LogInformation("[WCF] XML construído: {Xml}", xmlEvento.ToString());

            // 4. Assinar XML
            _logger.LogInformation("[WCF] Assinando XML digitalmente...");
            var idEvento = $"ID210210{request.ChaveNFe}01";
            var xmlAssinado = _signatureService.AssinarXml(xmlEvento, certificado, idEvento);

            _logger.LogInformation("[WCF] XML assinado com sucesso");
            _logger.LogInformation("[WCF] XML ASSINADO COMPLETO: {XmlAssinado}", xmlAssinado.ToString());

            // 5. Enviar para SEFAZ via WCF
            _logger.LogInformation("[WCF] Enviando para SEFAZ via WCF (System.ServiceModel)...");
            // cOrgao=91 (Ambiente Nacional) sempre usa SVRS, não usa UF da chave
            _logger.LogInformation("[WCF] Usando SVRS (Ambiente Nacional - cOrgao=91)");
            var xmlResposta = await _eventoClientWcf.EnviarEventoAsync(xmlAssinado, certificado, "SVRS");

            _logger.LogInformation("[WCF] Resposta recebida da SEFAZ: {Resposta}", xmlResposta);

            // 6. Processar resposta
            var response = ProcessarRespostaSefaz(xmlResposta);

            if (response.Success)
            {
                _logger.LogInformation("[WCF] Manifestação registrada com sucesso! Protocolo: {Protocolo}", response.Protocolo);
            }
            else
            {
                _logger.LogWarning("[WCF] Manifestação rejeitada. cStat: {CStat}, Motivo: {Motivo}", response.CStat, response.XMotivo);
            }

            return Ok(response);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "[WCF] Arquivo de certificado não encontrado");
            return NotFound(new ManifestacaoResponse
            {
                Success = false,
                Erro = $"Certificado não encontrado: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WCF] Erro ao manifestar ciência");
            return StatusCode(500, new ManifestacaoResponse
            {
                Success = false,
                Erro = $"Erro interno: {ex.Message}",
                XmlRetorno = ex.ToString() // Stack trace para debug
            });
        }
    }
}
