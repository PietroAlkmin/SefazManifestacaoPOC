using System.Xml.Linq;

namespace SefazManifestacaoPOC.Services;

/// <summary>
/// Constrói o XML do evento de manifestação de ciência da operação
/// </summary>
public class XmlEventoBuilder
{
    private const string NamespaceNFe = "http://www.portalfiscal.inf.br/nfe";

    /// <summary>
    /// Constrói o XML do evento de manifestação de ciência (código 210210)
    /// </summary>
    /// <param name="chaveNFe">Chave de acesso da NFe (44 dígitos)</param>
    /// <param name="cnpjDestinatario">CNPJ do destinatário (14 dígitos)</param>
    /// <param name="isProducao">true para ambiente de produção, false para homologação</param>
    /// <returns>XDocument com o XML do evento</returns>
    public XDocument ConstruirEventoCiencia(string chaveNFe, string cnpjDestinatario, bool isProducao = false)
    {
        if (string.IsNullOrWhiteSpace(chaveNFe) || chaveNFe.Length != 44)
            throw new ArgumentException("ChaveNFe deve ter 44 dígitos", nameof(chaveNFe));

        if (string.IsNullOrWhiteSpace(cnpjDestinatario) || cnpjDestinatario.Length != 14)
            throw new ArgumentException("CNPJ deve ter 14 dígitos", nameof(cnpjDestinatario));

        var tpAmb = isProducao ? 1 : 2; // 1=Produção, 2=Homologação
        // Formato de data SEFAZ: yyyy-MM-ddTHH:mm:ss-03:00 (ISO 8601 com timezone)
        var dhEvento = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ssK");
        
        var tpEvento = "210210"; // Confirmação da Operação
        var descEvento = "Confirmação da Operação"; // Deve corresponder ao tpEvento
        var idEvento = $"ID{tpEvento}{chaveNFe}01";
        
        // Extrair cUF dos 2 primeiros dígitos da chave (ex: 31 = MG)
        var cOrgao = chaveNFe.Substring(0, 2);

        XNamespace ns = NamespaceNFe;

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "envEvento",
                new XAttribute("versao", "1.00"),
                new XElement(ns + "idLote", "1"),
                new XElement(ns + "evento",
                    new XAttribute("versao", "1.00"),
                    new XElement(ns + "infEvento",
                        new XAttribute("Id", idEvento),
                        new XElement(ns + "cOrgao", cOrgao), // cUF do emitente (extraído da chave)
                        new XElement(ns + "tpAmb", tpAmb),
                        new XElement(ns + "CNPJ", cnpjDestinatario),
                        new XElement(ns + "chNFe", chaveNFe),
                        new XElement(ns + "dhEvento", dhEvento),
                        new XElement(ns + "tpEvento", tpEvento),
                        new XElement(ns + "nSeqEvento", "1"),
                        new XElement(ns + "verEvento", "1.00"),
                        new XElement(ns + "detEvento",
                            new XAttribute("versao", "1.00"),
                            // Elemento descEvento SEM namespace (conforme schema da SEFAZ)
                            new XElement("descEvento", descEvento)
                        )
                    )
                )
            )
        );

        return xml;
    }

    /// <summary>
    /// Extrai o código da UF dos 2 primeiros dígitos da chave da NFe
    /// </summary>
    public static string ExtrairUF(string chaveNFe)
    {
        if (string.IsNullOrWhiteSpace(chaveNFe) || chaveNFe.Length < 2)
            return "DEFAULT";

        var codigoUF = chaveNFe.Substring(0, 2);
        
        // Mapeamento de códigos para siglas de UF
        return codigoUF switch
        {
            "11" => "RO",
            "12" => "AC",
            "13" => "AM",
            "14" => "RR",
            "15" => "PA",
            "16" => "AP",
            "17" => "TO",
            "21" => "MA",
            "22" => "PI",
            "23" => "CE",
            "24" => "RN",
            "25" => "PB",
            "26" => "PE",
            "27" => "AL",
            "28" => "SE",
            "29" => "BA",
            "31" => "MG",
            "32" => "ES",
            "33" => "RJ",
            "35" => "SP",
            "41" => "PR",
            "42" => "SC",
            "43" => "RS",
            "50" => "MS",
            "51" => "MT",
            "52" => "GO",
            "53" => "DF",
            _ => "DEFAULT"
        };
    }

    /// <summary>
    /// Obtém o elemento infEvento para assinatura
    /// </summary>
    public XElement? ObterInfEvento(XDocument xmlDoc)
    {
        XNamespace ns = NamespaceNFe;
        return xmlDoc.Descendants(ns + "infEvento").FirstOrDefault();
    }
}
