using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace SefazManifestacaoPOC.Services;

/// <summary>
/// Cliente para envio de eventos para o serviço NFeRecepcaoEvento da SEFAZ
/// </summary>
public class SefazEventoClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    // URLs dos serviços de homologação por UF
    private static readonly Dictionary<string, string> UrlsHomologacao = new()
    {
        // SVRS - Estados que usam Sefaz Virtual do Rio Grande do Sul
        ["DEFAULT"] = "https://nfe-homologacao.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
        ["RS"] = "https://nfe-homologacao.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
        ["AC"] = "https://nfe-homologacao.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
        ["AL"] = "https://nfe-homologacao.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
        ["AP"] = "https://nfe-homologacao.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
        ["MG"] = "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeRecepcaoEvento4",
        ["SP"] = "https://homologacao.nfe.fazenda.sp.gov.br/ws/nferecepcaoevento4.asmx"
    };

    public SefazEventoClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Envia evento de manifestação para a SEFAZ
    /// </summary>
    /// <param name="xmlEvento">XML do evento assinado</param>
    /// <param name="certificado">Certificado digital</param>
    /// <param name="uf">UF para determinar o endpoint (padrão: SVRS)</param>
    /// <returns>XML de retorno da SEFAZ</returns>
    public async Task<string> EnviarEventoAsync(XDocument xmlEvento, X509Certificate2 certificado, string uf = "DEFAULT")
    {
        var url = ObterUrlServico(uf);
        // Remover declaração XML (<?xml...?>) pois vai dentro do SOAP
        var xmlString = xmlEvento.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces);
        if (xmlString.StartsWith("<?xml"))
        {
            xmlString = xmlString.Substring(xmlString.IndexOf("?>") + 2).TrimStart();
        }

        // Criar envelope SOAP
        var soapEnvelope = CriarEnvelopeSOAP(xmlString);

        // Configurar HttpClient com certificado
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(certificado);
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true; // Para homologação

        using var httpClient = new HttpClient(handler);
        httpClient.Timeout = TimeSpan.FromSeconds(60);

        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml")
        {
            CharSet = "utf-8"
        };

        // SOAPAction correto para NFeRecepcaoEvento (baseado no WSDL oficial)
        content.Headers.Add("SOAPAction", "\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento/nfeRecepcaoEvento\"");

        var response = await httpClient.PostAsync(url, content);
        
        // Não validar status code - SEFAZ pode retornar 500 com XML válido contendo erro
        var responseContent = await response.Content.ReadAsStringAsync();

        // Extrair XML da resposta SOAP
        return ExtrairXmlRespostaSOAP(responseContent);
    }

    private string ObterUrlServico(string uf)
    {
        if (UrlsHomologacao.TryGetValue(uf.ToUpper(), out var url))
            return url;

        return UrlsHomologacao["DEFAULT"];
    }

    private string CriarEnvelopeSOAP(string xmlBody)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <soap:Body>
        <nfeDadosMsg xmlns=""http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento"">
            {xmlBody}
        </nfeDadosMsg>
    </soap:Body>
</soap:Envelope>";
    }

    private string ExtrairXmlRespostaSOAP(string soapResponse)
    {
        try
        {
            var doc = XDocument.Parse(soapResponse);
            XNamespace soapNs = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace nfeNs = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento";

            var bodyElement = doc.Descendants(soapNs + "Body").FirstOrDefault();
            if (bodyElement != null)
            {
                var resultElement = bodyElement.Descendants(nfeNs + "nfeResultMsg").FirstOrDefault();
                if (resultElement != null)
                {
                    return resultElement.FirstNode?.ToString() ?? soapResponse;
                }
            }

            // Se não encontrou, retorna a resposta completa
            return soapResponse;
        }
        catch
        {
            return soapResponse;
        }
    }
}
