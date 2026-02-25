using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Xml;
using System.Xml.Linq;

namespace SefazManifestacaoPOC.Services;

/// <summary>
/// Cliente WCF para envio de eventos SEFAZ usando System.ServiceModel
/// Baseado no SefazStatusClient do Kapty.Core
/// </summary>
public class SefazEventoClientWcf
{
    private readonly ILogger<SefazEventoClientWcf> _logger;

    private static readonly Dictionary<string, string> UrlsProducao = new()
    {
        // ✅ SVRS Ambiente Nacional (cOrgao=91) - DEFAULT para todas UFs - PRODUÇÃO
        // WCF interoperável com ASMX (.NET), evita problemas com JAX-WS (Java)
        ["DEFAULT"] = "https://nfe.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
        ["SVRS"] = "https://nfe.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
        ["91"] = "https://nfe.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx", // Ambiente Nacional
        
        // Endpoints específicos por UF - PRODUÇÃO
        ["RS"] = "https://nfe.svrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
        ["MG"] = "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeRecepcaoEvento4", // JAX-WS - WCF incompatível
        ["SP"] = "https://nfe.fazenda.sp.gov.br/ws/nferecepcaoevento4.asmx",
        ["BA"] = "https://nfe.sefaz.ba.gov.br/webservices/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx",
        ["PR"] = "https://nfe.sefa.pr.gov.br/nfe/NFeRecepcaoEvento4",
        ["GO"] = "https://nfe.sefaz.go.gov.br/nfe/services/NFeRecepcaoEvento4",
        ["AM"] = "https://nfe.sefaz.am.gov.br/services2/services/RecepcaoEvento4",
        ["MT"] = "https://nfe.sefaz.mt.gov.br/nfews/v2/services/RecepcaoEvento4"
    };

    public SefazEventoClientWcf(ILogger<SefazEventoClientWcf> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Envia evento usando WCF/System.ServiceModel (lida automaticamente com SOAP)
    /// </summary>
    public async Task<string> EnviarEventoAsync(XDocument xmlEvento, X509Certificate2 certificado, string uf = "DEFAULT")
    {
        var url = ObterUrlServico(uf);
        _logger.LogInformation("Enviando evento via WCF para {UF}: {Url}", uf, url);

        // Converter XDocument para XmlNode usando método de extensão
        var xmlDoc = xmlEvento.ToXmlDocument();
        var xmlNode = xmlDoc.DocumentElement!;

        // Criar binding e endpoint
        var binding = NFeRecepcaoEvento4SoapClient.CreateBinding();
        var endpoint = new EndpointAddress(url);

        // Criar client WCF
        var client = new NFeRecepcaoEvento4SoapClient(binding, endpoint);

        try
        {
            // Configurar certificado (baseado em SefazStatusClient.ConfigureClient)
            ConfigureClient(client, certificado);

            _logger.LogInformation("Invocando nfeRecepcaoEventoAsync...");

            // Enviar requisição SOAP (WCF monta o envelope automaticamente!)
            var response = await client.nfeRecepcaoEventoAsync(xmlNode);

            if (response?.nfeResultMsg != null)
            {
                var responseXml = response.nfeResultMsg.OuterXml;
                _logger.LogInformation("Resposta WCF recebida: {Length} chars", responseXml.Length);
                return responseXml;
            }

            _logger.LogWarning("Resposta WCF vazia");
            return "<erro>Resposta vazia da SEFAZ</erro>";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar via WCF: {Message}", ex.Message);
            throw;
        }
        finally
        {
            CloseClient(client);
        }
    }

    private string ObterUrlServico(string uf)
    {
        if (UrlsProducao.TryGetValue(uf.ToUpper(), out var url))
            return url;

        return UrlsProducao["DEFAULT"];
    }

    private void ConfigureClient(ClientBase<INFeRecepcaoEvento4Soap> client, X509Certificate2 certificate)
    {
        client.ClientCredentials.ClientCertificate.Certificate = certificate;
        client.ClientCredentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.None;
        client.ClientCredentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;

        if (client.Endpoint.Binding is CustomBinding customBinding)
        {
            foreach (var element in customBinding.Elements)
            {
                if (element is HttpsTransportBindingElement https)
                {
                    https.RequireClientCertificate = true;
                }
            }
        }
        else if (client.Endpoint.Binding is BasicHttpBinding basicBinding)
        {
            basicBinding.Security.Mode = BasicHttpSecurityMode.Transport;
            basicBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Certificate;
        }
    }

    private void CloseClient(ICommunicationObject client)
    {
        try
        {
            if (client.State == CommunicationState.Faulted)
            {
                client.Abort();
            }
            else
            {
                client.Close();
            }
        }
        catch
        {
            client.Abort();
        }
    }
}
