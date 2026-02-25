using System.Xml;
using System.Xml.Linq;

namespace SefazManifestacaoPOC.Services;

/// <summary>
/// Helper para conversões entre XDocument e XmlDocument
/// </summary>
public static class XmlHelper
{
    /// <summary>
    /// Converte XDocument para XmlDocument preservando namespaces e estrutura
    /// </summary>
    public static XmlDocument ToXmlDocument(this XDocument xDocument)
    {
        var xmlDoc = new XmlDocument();
        using (var xmlReader = xDocument.CreateReader())
        {
            xmlDoc.Load(xmlReader);
        }
        return xmlDoc;
    }

    /// <summary>
    /// Converte XmlDocument para XDocument
    /// </summary>
    public static XDocument ToXDocument(this XmlDocument xmlDocument)
    {
        using (var nodeReader = new XmlNodeReader(xmlDocument))
        {
            nodeReader.MoveToContent();
            return XDocument.Load(nodeReader);
        }
    }
}
