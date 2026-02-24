namespace SefazManifestacaoPOC.Models;

public class ManifestacaoResponse
{
    /// <summary>
    /// Indica se a manifestação foi bem-sucedida
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Código de status retornado pela SEFAZ
    /// 135 = Evento registrado e vinculado a NF-e
    /// </summary>
    public string? CStat { get; set; }

    /// <summary>
    /// Descrição do motivo/resultado
    /// </summary>
    public string? XMotivo { get; set; }

    /// <summary>
    /// Número do protocolo de registro do evento
    /// </summary>
    public string? Protocolo { get; set; }

    /// <summary>
    /// Data e hora do registro do evento
    /// </summary>
    public DateTime? DataRegistro { get; set; }

    /// <summary>
    /// XML completo da resposta da SEFAZ
    /// </summary>
    public string? XmlRetorno { get; set; }

    /// <summary>
    /// Mensagem de erro (quando houver)
    /// </summary>
    public string? Erro { get; set; }
}
