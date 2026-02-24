using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;

namespace SefazManifestacaoPOC.Services;

/// <summary>
/// Assina digitalmente XMLs usando certificado digital A1
/// </summary>
public class XmlSignatureService
{
    /// <summary>
    /// Assina o XML do evento no elemento infEvento
    /// </summary>
    /// <param name="xmlDoc">XDocument com o XML a ser assinado</param>
    /// <param name="certificado">Certificado digital com chave privada</param>
    /// <param name="elementoId">ID do elemento a ser assinado (ex: ID210210...)</param>
    /// <returns>XDocument assinado</returns>
    public XDocument AssinarXml(XDocument xmlDoc, X509Certificate2 certificado, string elementoId)
    {
        if (!certificado.HasPrivateKey)
            throw new InvalidOperationException("Certificado não possui chave privada");

        // Converter XDocument para XmlDocument
        var xmlDocument = new XmlDocument { PreserveWhitespace = true };
        using (var reader = xmlDoc.CreateReader())
        {
            xmlDocument.Load(reader);
        }

        // Localizar o elemento a ser assinado pelo atributo Id
        var elementoAssinado = xmlDocument.GetElementById(elementoId);
        if (elementoAssinado == null)
        {
            // Fallback: buscar por Id manualmente
            var nsmgr = new XmlNamespaceManager(xmlDocument.NameTable);
            nsmgr.AddNamespace("nfe", "http://www.portalfiscal.inf.br/nfe");
            elementoAssinado = xmlDocument.SelectSingleNode($"//nfe:infEvento[@Id='{elementoId}']", nsmgr) as XmlElement;
        }

        if (elementoAssinado == null)
            throw new InvalidOperationException($"Elemento com Id '{elementoId}' não encontrado");

        // Criar SignedXml com contexto do documento (não do elemento)
        var signedXml = new SignedXml(xmlDocument)
        {
            SigningKey = certificado.GetRSAPrivateKey() ?? throw new InvalidOperationException("Não foi possível obter chave RSA")
        };

        // Criar referência ao elemento
        var reference = new Reference($"#{elementoId}");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform(false));
        signedXml.AddReference(reference);

        // Adicionar KeyInfo com certificado
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificado));
        signedXml.KeyInfo = keyInfo;

        // Computar assinatura
        signedXml.ComputeSignature();

        // Obter XML da assinatura
        var xmlSignature = signedXml.GetXml();

        // Inserir assinatura após infEvento (dentro de evento)
        var eventoNode = elementoAssinado.ParentNode;
        if (eventoNode != null)
        {
            eventoNode.AppendChild(xmlDocument.ImportNode(xmlSignature, true));
        }

        // Converter de volta para XDocument
        using (var nodeReader = new XmlNodeReader(xmlDocument))
        {
            return XDocument.Load(nodeReader);
        }
    }

    /// <summary>
    /// Carrega certificado digital do arquivo .pfx
    /// </summary>
    public X509Certificate2 CarregarCertificado(string caminhoArquivo, string senha)
    {
        if (!File.Exists(caminhoArquivo))
            throw new FileNotFoundException("Arquivo de certificado não encontrado", caminhoArquivo);

        var certificado = new X509Certificate2(
            caminhoArquivo,
            senha,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable
        );

        if (!certificado.HasPrivateKey)
            throw new InvalidOperationException("Certificado não possui chave privada");

        return certificado;
    }
}
