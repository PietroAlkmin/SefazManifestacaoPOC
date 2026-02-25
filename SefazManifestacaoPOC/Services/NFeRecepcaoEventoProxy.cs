using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace SefazManifestacaoPOC.Services;

/// <summary>
/// Proxy WCF gerado do WSDL oficial da SEFAZ para NFeRecepcaoEvento4
/// Adaptado do Kapty.Core.Infra.Sefaz
/// </summary>
[ServiceContract(Namespace = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4")]
public interface INFeRecepcaoEvento4Soap
{
    [OperationContract(Action = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4/nfeRecepcaoEvento", ReplyAction = "*")]
    [XmlSerializerFormat(SupportFaults = true)]
    Task<NFeRecepcaoEventoResponse> nfeRecepcaoEventoAsync(NFeRecepcaoEventoRequest request);
}

[MessageContract(IsWrapped = false)]
public class NFeRecepcaoEventoRequest
{
    [MessageBodyMember(Namespace = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4", Order = 0)]
    public XmlNode? nfeDadosMsg;

    public NFeRecepcaoEventoRequest()
    {
    }

    public NFeRecepcaoEventoRequest(XmlNode nfeDadosMsg)
    {
        this.nfeDadosMsg = nfeDadosMsg;
    }
}

[MessageContract(IsWrapped = false)]
public class NFeRecepcaoEventoResponse
{
    [MessageBodyMember(Namespace = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4", Order = 0)]
    public XmlNode? nfeResultMsg;

    public NFeRecepcaoEventoResponse()
    {
    }

    public NFeRecepcaoEventoResponse(XmlNode nfeResultMsg)
    {
        this.nfeResultMsg = nfeResultMsg;
    }
}

public class NFeRecepcaoEvento4SoapClient : ClientBase<INFeRecepcaoEvento4Soap>, INFeRecepcaoEvento4Soap
{
    public NFeRecepcaoEvento4SoapClient(Binding binding, EndpointAddress remoteAddress) 
        : base(binding, remoteAddress)
    {
    }

    public static Binding CreateBinding()
    {
        // BasicHttpBinding com SOAP 1.1 e HTTPS
        var result = new BasicHttpBinding
        {
            MaxBufferSize = int.MaxValue,
            ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
            MaxReceivedMessageSize = int.MaxValue,
            AllowCookies = true,
            Security = { Mode = BasicHttpSecurityMode.Transport }
        };
        return result;
    }

    Task<NFeRecepcaoEventoResponse> INFeRecepcaoEvento4Soap.nfeRecepcaoEventoAsync(NFeRecepcaoEventoRequest request)
    {
        return Channel.nfeRecepcaoEventoAsync(request);
    }

    public Task<NFeRecepcaoEventoResponse> nfeRecepcaoEventoAsync(XmlNode nfeDadosMsg)
    {
        var request = new NFeRecepcaoEventoRequest(nfeDadosMsg);
        return ((INFeRecepcaoEvento4Soap)this).nfeRecepcaoEventoAsync(request);
    }
}
