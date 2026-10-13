using CennoTV.Core;
using CennoTV.Core.Navegacao;

namespace CennoTV.Core.Tests;

public class NavegacaoGradeTests
{
    // Grade de 7 blocos em 4 colunas:
    //  0 1 2 3
    //  4 5 6
    [Fact]
    public void Comeca_no_primeiro_bloco()
    {
        var g = new NavegacaoGrade(7, 4);
        Assert.Equal(0, g.Indice);
    }

    [Fact]
    public void Nao_da_a_volta_nas_bordas()
    {
        var g = new NavegacaoGrade(7, 4);
        Assert.False(g.Mover(ComandoTv.Esquerda));
        Assert.False(g.Mover(ComandoTv.Cima));
        g.IrPara(3);
        Assert.False(g.Mover(ComandoTv.Direita));
        Assert.Equal(3, g.Indice);
    }

    [Fact]
    public void Descer_para_linha_mais_curta_cai_no_ultimo_bloco()
    {
        var g = new NavegacaoGrade(7, 4);
        g.IrPara(3);
        Assert.True(g.Mover(ComandoTv.Baixo));
        Assert.Equal(6, g.Indice);
    }

    [Fact]
    public void Descer_na_coluna_que_existe_mantem_a_coluna()
    {
        var g = new NavegacaoGrade(7, 4);
        g.IrPara(1);
        Assert.True(g.Mover(ComandoTv.Baixo));
        Assert.Equal(5, g.Indice);
        Assert.True(g.Mover(ComandoTv.Cima));
        Assert.Equal(1, g.Indice);
    }

    [Fact]
    public void Na_ultima_linha_nao_desce()
    {
        var g = new NavegacaoGrade(7, 4);
        g.IrPara(5);
        Assert.False(g.Mover(ComandoTv.Baixo));
    }

    [Fact]
    public void Direita_no_ultimo_bloco_da_linha_curta_nao_anda()
    {
        var g = new NavegacaoGrade(7, 4);
        g.IrPara(6);
        Assert.False(g.Mover(ComandoTv.Direita));
    }

    [Fact]
    public void Comandos_que_nao_sao_setas_nao_mexem_no_foco()
    {
        var g = new NavegacaoGrade(7, 4);
        g.IrPara(2);
        Assert.False(g.Mover(ComandoTv.Ok));
        Assert.False(g.Mover(ComandoTv.VolumeMais));
        Assert.Equal(2, g.Indice);
    }

    [Fact]
    public void Grade_vazia_nao_tem_foco_e_nao_quebra()
    {
        var g = new NavegacaoGrade(0, 4);
        Assert.Equal(-1, g.Indice);
        Assert.False(g.Mover(ComandoTv.Direita));
        g.IrPara(3);
        Assert.Equal(-1, g.Indice);
    }

    [Fact]
    public void IrPara_limita_ao_intervalo()
    {
        var g = new NavegacaoGrade(7, 4);
        g.IrPara(99);
        Assert.Equal(6, g.Indice);
        g.IrPara(-5);
        Assert.Equal(0, g.Indice);
    }

    [Fact]
    public void Colunas_invalidas_sao_recusadas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavegacaoGrade(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavegacaoGrade(-1, 4));
    }
}
