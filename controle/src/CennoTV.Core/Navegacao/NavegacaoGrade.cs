namespace CennoTV.Core.Navegacao;

/// <summary>
/// Foco numa grade de blocos, como no controle de uma TV. Lógica pura, sem WPF, para ser testada
/// e usada igual pelo teclado, pelo gamepad e pelo controle remoto.
/// Não dá a volta nas bordas. Descer para uma última linha mais curta cai no último bloco.
/// </summary>
public sealed class NavegacaoGrade
{
    public NavegacaoGrade(int totalItens, int colunas)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalItens);
        ArgumentOutOfRangeException.ThrowIfLessThan(colunas, 1);
        TotalItens = totalItens;
        Colunas = colunas;
        Indice = totalItens == 0 ? -1 : 0;
    }

    public int TotalItens { get; }
    public int Colunas { get; }

    /// <summary>Bloco em foco; -1 quando a grade está vazia.</summary>
    public int Indice { get; private set; }

    public int Linha => Indice < 0 ? -1 : Indice / Colunas;
    public int Coluna => Indice < 0 ? -1 : Indice % Colunas;

    /// <summary>Move o foco. Devolve true se o foco mudou de bloco.</summary>
    public bool Mover(ComandoTv comando)
    {
        if (TotalItens == 0) return false;

        int destino = comando switch
        {
            ComandoTv.Esquerda when Coluna > 0 => Indice - 1,
            ComandoTv.Direita when Coluna < Colunas - 1 && Indice + 1 < TotalItens => Indice + 1,
            ComandoTv.Cima when Linha > 0 => Indice - Colunas,
            ComandoTv.Baixo when (Linha + 1) * Colunas < TotalItens => Math.Min(Indice + Colunas, TotalItens - 1),
            _ => Indice,
        };

        if (destino == Indice) return false;
        Indice = destino;
        return true;
    }

    public void IrPara(int indice)
    {
        if (TotalItens == 0) return;
        Indice = Math.Clamp(indice, 0, TotalItens - 1);
    }
}
