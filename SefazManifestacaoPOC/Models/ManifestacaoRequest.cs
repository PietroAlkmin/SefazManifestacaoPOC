namespace SefazManifestacaoPOC.Models;

public class ManifestacaoRequest
{
    /// <summary>
    /// Chave de acesso da NFe (44 dígitos)
    /// Exemplo: 31260242831263000192550010001096711000000052
    /// </summary>
    public required string ChaveNFe { get; set; }

    /// <summary>
    /// CNPJ do destinatário que recebeu o ResNFe (14 dígitos)
    /// </summary>
    public required string CnpjDestinatario { get; set; }

    /// <summary>
    /// Caminho completo do arquivo certificado digital (.pfx)
    /// </summary>
    public required string CertificadoPath { get; set; }

    /// <summary>
    /// Senha do certificado digital
    /// </summary>
    public required string CertificadoSenha { get; set; }
}
