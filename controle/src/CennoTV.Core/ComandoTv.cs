namespace CennoTV.Core;

/// <summary>
/// Comandos de TV que a interface aceita. O controle remoto só pode mandar estes, mais os poucos
/// tipos fechados de <see cref="Controle.MensagemControle"/> (abrir item do catálogo pelo id, mouse,
/// texto digitado como teclas): nunca caminho de arquivo nem comando de shell.
/// </summary>
public enum ComandoTv
{
    Cima,
    Baixo,
    Esquerda,
    Direita,
    Ok,
    Voltar,
    Inicio,
    VolumeMais,
    VolumeMenos,
    Mudo,
    PlayPausa,
    TelaCheia,
    TrocarJanela,
    Fechar,
    /// <summary>Backspace: apaga o último caractere (aba Teclado do controle).</summary>
    Apagar,
}
