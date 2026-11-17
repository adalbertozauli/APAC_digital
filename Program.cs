using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using Projeto_APAC.Models;
using Projeto_APAC.Services;

namespace Projeto_APAC
{
    public class FormPrincipal : Form, IFundoPintavel
    {
        // ---------- Controles ----------
        private TextBox txtNome = null!, txtDataNasc = null!, txtMae = null!, txtCid = null!, txtDiagnostico = null!, txtProcedimento = null!, txtObs = null!;
        private ComboBox cbSexo = null!, cbRaca = null!, cbQuantidade = null!, cbProfissional = null!;
        private LinkLabel lnkNovoProf = null!, lnkEditarProf = null!, lnkExcluirProf = null!;
        private List<Profissional> profissionais = new List<Profissional>();
        private BotaoModerno btnGerar = null!, btnLimpar = null!, btnAlternarTema = null!;
        private Label lblTituloApp = null!, lblAvisoCid = null!, lblAtalhos = null!;
        private RotuloStatus lblStatus = null!;
        private LinkLabel lnkPasta = null!, lnkMesmoPaciente = null!;
        private ListBox lstSugestoes = null!;
        private readonly ToolTip dicas = new ToolTip();

        // Cada campo fica dentro de uma "caixa" com borda (foco/erro)
        private readonly Dictionary<Control, CaixaCampo> caixas = new Dictionary<Control, CaixaCampo>();

        // ---------- Estado ----------
        private bool isDarkMode;
        private string? diagnosticoAutomatico;   // último diagnóstico preenchido pela busca de CID
        private bool formatandoData;
        private bool preenchendoCid;              // evita reabrir sugestões ao preencher via código
        private bool inicializado;                // evita salvar preferências durante a montagem da tela
        private bool alteradoDesdeUltimoLaudo;    // há dados digitados que ainda não viraram PDF
        private bool limpandoFormulario;
        private readonly List<(string Codigo, string Descricao, string Busca, string CodigoSemPonto, string DescricaoNorm)> cids = new List<(string, string, string, string, string)>();

        private Tema tema = Tema.Escuro;

        // Declarado antes de PastaLaudos: inicializadores estáticos rodam na ordem do arquivo
        private static readonly string ArquivoPreferencias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "APAC Digital", "preferencias.txt");

        // Pasta escolhida no instalador (preferência "pastaLaudos"); sem ela, Documentos\APAC Digital\Laudos
        private static readonly string? pastaLaudosEscolhida = LerPreferencia("pastaLaudos") is { Length: > 0 } p ? p : null;

        internal static string PastaLaudos { get; set; } = pastaLaudosEscolhida ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "APAC Digital", "Laudos");

        internal static bool AbrirPdfAoGerar { get; set; } = true;

        private const int Margem = 30;
        private const int LarguraUtil = 465;

        public FormPrincipal()
        {
            isDarkMode = CarregarTemaPreferido();
            CarregarCids();
            profissionais = RepositorioProfissionais.Carregar();
            ConfigurarJanela();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            CriarInterface();
            ResumeLayout(false);
            AplicarTema();
            MigrarLaudosAntigos();
            inicializado = true;
            Shown += (s, e) => {
                AplicarTema();
                txtNome.Select();
                // Primeiro uso: nenhum profissional vem cadastrado
                if (profissionais.Count == 0)
                    MostrarStatus("Comece cadastrando o(a) médico(a) solicitante em \"+ Cadastrar novo\".", tema.Aviso);
            };
        }

        // =====================================================================
        //  Janela e interface
        // =====================================================================

        private void ConfigurarJanela()
        {
            Text = "APAC Digital - ESF São Carlos 2";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            KeyPreview = true;
            Font = new Font("Segoe UI", 9F);

            string caminhoIcone = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icone.ico");
            if (File.Exists(caminhoIcone)) Icon = new Icon(caminhoIcone);
        }

        private void CriarInterface()
        {
            DoubleBuffered = true;

            // --- Cabeçalho: painel de vidro (desenhado no fundo) com logo, título e botão de tema ---
            lblTituloApp = new Label {
                Text = "Emissão de Laudo APAC",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(AreaLogo.Right + 10, 15)
            };
            lblSubtitulo = new Label {
                Text = "APAC DIGITAL   •   ESF SÃO CARLOS 2",
                Font = new Font("Segoe UI", 8F),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(AreaLogo.Right + 13, 49),
                Tag = "mutado"
            };
            btnAlternarTema = new BotaoModerno {
                Estilo = BotaoModerno.Estilos.Icone,
                Size = new Size(38, 38),
                Location = new Point(Margem + LarguraUtil - 38 + 4, 24),
                Raio = 11,
                TabStop = false
            };
            btnAlternarTema.Click += (s, e) => { isDarkMode = !isDarkMode; temaEscolhido = true; SalvarPreferencias(); AplicarTema(); };
            dicas.SetToolTip(btnAlternarTema, "Alternar tema claro / escuro");
            Controls.Add(lblTituloApp);
            Controls.Add(lblSubtitulo);
            Controls.Add(btnAlternarTema);

            // Lista de sugestões de CID (flutua sobre os campos)
            lstSugestoes = new ListBox {
                Visible = false,
                TabStop = false,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                ItemHeight = 20
            };
            lstSugestoes.MouseClick += (s, e) => {
                int i = lstSugestoes.IndexFromPoint(e.Location);
                if (i >= 0) SelecionarSugestao(i);
            };
            Controls.Add(lstSugestoes);

            int y = TopoCartao + 12;
            CriarSecao("PROFISSIONAL SOLICITANTE", Visual.IconeSaude, ref y);

            cbProfissional = CriarCombo("Médico(a) solicitante *", Margem, y, LarguraUtil, Array.Empty<string>(), Visual.IconeContato);
            cbProfissional.SelectedIndexChanged += (s, e) => { AtualizarAcoesProfissional(); SalvarPreferencias(); };
            // Links na linha do rótulo, alinhados à direita (criados da esquerda p/ direita para a ordem do Tab)
            var fonteLink = new Font("Segoe UI", 8.5F);
            string[] textosLinks = { "+ Cadastrar novo", "Editar", "Excluir" };
            int xLink = Margem + LarguraUtil - textosLinks.Sum(t => TextRenderer.MeasureText(t, fonteLink).Width) - 10 * (textosLinks.Length - 1);
            lnkNovoProf = CriarLinkAcao(textosLinks[0], ref xLink, y, CadastrarProfissional);
            lnkEditarProf = CriarLinkAcao(textosLinks[1], ref xLink, y, EditarProfissional);
            lnkExcluirProf = CriarLinkAcao(textosLinks[2], ref xLink, y, ExcluirProfissional);
            PreencherProfissionais(LerPreferencia("profissional"));
            y += PassoLinha + 6;

            CriarSecao("DADOS DO PACIENTE", Visual.IconeContato, ref y);

            txtNome = CriarTexto("Nome completo *", Margem, y, LarguraUtil, "Ex: JOÃO DA SILVA", icone: Visual.IconeCartao);
            y += PassoLinha;

            txtDataNasc = CriarTexto("Nascimento *", Margem, y, 130, "dd/mm/aaaa", icone: Visual.IconeCalendario);
            txtDataNasc.MaxLength = 10;
            txtDataNasc.CharacterCasing = CharacterCasing.Normal;
            txtDataNasc.TextChanged += (s, e) => FormatarData(txtDataNasc);
            cbSexo = CriarCombo("Sexo *", 172, y, 90, new[] { "M", "F" });
            cbRaca = CriarCombo("Raça/Cor", 274, y, 221, new[] { "BRANCA", "PRETA", "PARDA", "AMARELA", "INDÍGENA" });
            y += PassoLinha;

            txtMae = CriarTexto("Nome da mãe *", Margem, y, LarguraUtil, "Ex: MARIA DA SILVA", icone: Visual.IconePessoas);
            y += PassoLinha + 6;

            CriarSecao("INFORMAÇÕES CLÍNICAS", Visual.IconeDiagnostico, ref y);

            txtProcedimento = CriarTexto("Procedimento principal *", Margem, y, 380, "Ex: TC DE CRÂNIO", icone: Visual.IconeDocumento);
            cbQuantidade = CriarCombo("Qtd", 422, y, 73, new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" });
            cbQuantidade.SelectedIndex = 0;
            y += PassoLinha;

            txtCid = CriarTexto("CID-10 *", Margem, y, 108, "Ex: G30", icone: Visual.IconeEtiqueta);
            txtDiagnostico = CriarTexto("Diagnóstico (busque por código ou nome)", 150, y, 345, "Digite para buscar...", icone: Visual.IconeBusca);
            lblAvisoCid = new Label {
                AutoSize = true,
                Location = new Point(Margem, y + 20 + AlturaCampo + 1),
                Font = new Font("Segoe UI", 8F),
                BackColor = Color.Transparent,
                Visible = false,
                Tag = "aviso"
            };
            Controls.Add(lblAvisoCid);
            ConfigurarBuscaCid(txtCid);
            ConfigurarBuscaCid(txtDiagnostico);
            y += PassoLinha + 14;

            txtObs = CriarTexto("Observações / justificativa", Margem, y, LarguraUtil, "Justificativa clínica para o procedimento...", altura: 70, icone: Visual.IconeNota);
            y += 20 + 70 + 10;

            btnGerar = new BotaoModerno {
                Text = "Gerar e abrir PDF",
                Icone = Visual.IconeDocumento,
                Estilo = BotaoModerno.Estilos.Primario,
                Location = new Point(Margem, y),
                Size = new Size(300, 44),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
            };
            btnGerar.Click += BtnGerar_Click;
            dicas.SetToolTip(btnGerar, "Ctrl+Enter  ·  Enter passa para o próximo campo");
            Controls.Add(btnGerar);

            btnLimpar = new BotaoModerno {
                Text = "Limpar",
                Icone = Visual.IconeBorracha,
                Estilo = BotaoModerno.Estilos.Secundario,
                Location = new Point(Margem + 312, y),
                Size = new Size(LarguraUtil - 312, 44),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnLimpar.Click += BtnLimpar_Click;
            Controls.Add(btnLimpar);
            y += 44 + 8;

            lblStatus = new RotuloStatus {
                AutoSize = false,
                Location = new Point(Margem, y),
                Size = new Size(LarguraUtil, 26),
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(lblStatus);
            y += 26 + 8;

            lnkPasta = CriarLinkRodape("Abrir pasta dos laudos", Visual.IconePasta, Margem, y);
            lnkPasta.LinkClicked += (s, e) => AbrirPastaLaudos();

            lnkMesmoPaciente = CriarLinkRodape("Novo laudo, mesmo paciente", Visual.IconeCopiar,
                Margem + 16 + TextRenderer.MeasureText(lnkPasta.Text, lnkPasta.Font).Width + 14, y);
            lnkMesmoPaciente.LinkClicked += (s, e) => NovoLaudoMesmoPaciente();
            dicas.SetToolTip(lnkMesmoPaciente, "Mantém profissional e paciente; limpa procedimento, CID e justificativa (Ctrl+N)");

            lblAtalhos = new Label {
                Text = "Ctrl+Enter: gerar PDF",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F),
                BackColor = Color.Transparent,
                Tag = "mutado"
            };
            Controls.Add(lblAtalhos);
            lblAtalhos.Location = new Point(Margem + LarguraUtil - TextRenderer.MeasureText(lblAtalhos.Text, lblAtalhos.Font).Width, y + 1);
            y += 20;

            AreaCartao = new Rectangle(14, TopoCartao, Margem * 2 + LarguraUtil - 28, y + 10 - TopoCartao);
            ClientSize = new Size(Margem * 2 + LarguraUtil, y + 24);
            lstSugestoes.BringToFront();
        }

        // Geometria do visual (em pixels a 100% de escala; o fundo multiplica pela escala do Windows)
        private const int AlturaCampo = 34, PassoLinha = 20 + AlturaCampo + 6, TopoCartao = 88;
        private static readonly Rectangle AreaCabecalho = new Rectangle(14, 10, 497, 68);
        private static readonly Rectangle AreaLogo = new Rectangle(28, 22, 44, 44);
        private Rectangle AreaCartao;
        private readonly List<(Rectangle Area, string Icone)> iconesSecao = new List<(Rectangle, string)>();
        private readonly List<(Rectangle Area, string Icone)> iconesRotulo = new List<(Rectangle, string)>();
        private Label lblSubtitulo = null!;
        private Bitmap? fundoCache;

        private void CriarSecao(string titulo, string icone, ref int y)
        {
            iconesSecao.Add((new Rectangle(Margem, y, 24, 24), icone));
            var lbl = new Label {
                Text = titulo, Location = new Point(Margem + 32, y + 4), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), BackColor = Color.Transparent, Tag = "secao"
            };
            Controls.Add(lbl);
            y += 32;
        }

        // Link pequeno na linha do rótulo; avança x para o próximo link
        private LinkLabel CriarLinkAcao(string texto, ref int x, int y, Action acao)
        {
            var lnk = new LinkLabel {
                Text = texto, AutoSize = true, Font = new Font("Segoe UI", 8.5F),
                LinkBehavior = LinkBehavior.HoverUnderline, TabStop = true, BackColor = Color.Transparent
            };
            lnk.Location = new Point(x, y);
            x += TextRenderer.MeasureText(texto, lnk.Font).Width + 10;
            lnk.LinkClicked += (s, e) => acao();
            Controls.Add(lnk);
            return lnk;
        }

        private LinkLabel CriarLinkRodape(string texto, string icone, int x, int y)
        {
            iconesRotulo.Add((new Rectangle(x, y, 15, 17), icone));
            var lnk = new LinkLabel {
                Text = texto, AutoSize = true, Location = new Point(x + 16, y), Font = new Font("Segoe UI", 9F),
                LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Color.Transparent
            };
            Controls.Add(lnk);
            return lnk;
        }

        private Label CriarRotulo(string texto, int x, int y, string? icone = null)
        {
            int deslocamento = 0;
            if (icone != null) { iconesRotulo.Add((new Rectangle(x, y, 14, 17), icone)); deslocamento = 18; }
            var lbl = new Label {
                Text = texto, Location = new Point(x - 2 + deslocamento, y), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), BackColor = Color.Transparent
            };
            Controls.Add(lbl);
            return lbl;
        }

        private TextBox CriarTexto(string rotulo, int x, int y, int largura, string placeholder, int altura = AlturaCampo, string? icone = null)
        {
            CriarRotulo(rotulo, x, y, icone);
            bool multilinha = altura > AlturaCampo;
            var tb = new TextBox {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10F),
                PlaceholderText = placeholder,
                CharacterCasing = CharacterCasing.Upper,   // o laudo sai em maiúsculas: mostra igual na tela
                Multiline = multilinha,
                Dock = DockStyle.Fill
            };
            if (multilinha) tb.ScrollBars = ScrollBars.Vertical;

            var caixa = new CaixaCampo {
                Location = new Point(x, y + 20),
                Size = new Size(largura, altura),
                Padding = multilinha ? new Padding(10, 8, 5, 6) : new Padding(12, 8, 10, 3)
            };
            caixa.Controls.Add(tb);
            caixa.Click += (s, e) => tb.Select();
            RegistrarCaixa(tb, caixa);
            tb.TextChanged += (s, e) => { caixa.Erro = false; MarcarAlterado(); };
            return tb;
        }

        private ComboBox CriarCombo(string rotulo, int x, int y, int largura, string[] itens, string? icone = null)
        {
            CriarRotulo(rotulo, x, y, icone);
            var cb = new ComboTema {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 20,
                Font = new Font("Segoe UI", 10F),
                Dock = DockStyle.Fill
            };
            cb.Items.AddRange(itens);
            var caixa = new CaixaCampo {
                Location = new Point(x, y + 20),
                Size = new Size(largura, AlturaCampo),
                Padding = new Padding(6, 4, 8, 4)
            };
            caixa.Controls.Add(cb);
            RegistrarCaixa(cb, caixa);
            cb.SelectedIndexChanged += (s, e) => { caixa.Erro = false; if (cb != cbProfissional) MarcarAlterado(); };
            return cb;
        }

        private void MarcarAlterado()
        {
            if (inicializado && !limpandoFormulario) alteradoDesdeUltimoLaudo = true;
        }

        // Aviso ao fechar com dados que ainda não viraram laudo
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && alteradoDesdeUltimoLaudo && AlgumCampoPreenchido() &&
                MessageBox.Show("Há dados preenchidos que ainda não foram gerados em laudo.\n\nFechar mesmo assim?", "Fechar APAC Digital",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        private void RegistrarCaixa(Control campo, CaixaCampo caixa)
        {
            caixas[campo] = caixa;
            campo.Enter += (s, e) => caixa.Focado = true;
            campo.Leave += (s, e) => caixa.Focado = false;
            Controls.Add(caixa);
        }

        // =====================================================================
        //  Fundo: degradê, painéis de vidro, ícones das seções
        // =====================================================================

        protected override void OnPaintBackground(PaintEventArgs e) => PintarFundo(e.Graphics);

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            fundoCache?.Dispose(); fundoCache = null;
            Invalidate(true);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            fundoCache?.Dispose(); fundoCache = null;
            base.OnFormClosed(e);
        }

        public void PintarFundo(Graphics g)
        {
            var tam = ClientSize;
            if (tam.Width <= 0 || tam.Height <= 0) return;
            if (fundoCache == null || fundoCache.Size != tam) { fundoCache?.Dispose(); fundoCache = GerarFundo(tam); }
            g.DrawImageUnscaled(fundoCache, 0, 0);
        }

        private Bitmap GerarFundo(Size tam)
        {
            var bmp = new Bitmap(tam.Width, tam.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float f = DeviceDpi / 96f;
            RectangleF S(Rectangle r) => new RectangleF(r.X * f, r.Y * f, r.Width * f, r.Height * f);

            var todo = new Rectangle(Point.Empty, tam);
            using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(todo, tema.FundoTopo, tema.FundoBase, 70f))
                g.FillRectangle(lg, todo);
            Bolha(g, new RectangleF(tam.Width * 0.35f, -tam.Height * 0.25f, tam.Width * 1.1f, tam.Height * 0.7f), tema.Bolha1);
            Bolha(g, new RectangleF(-tam.Width * 0.6f, tam.Height * 0.55f, tam.Width * 1.2f, tam.Height * 0.6f), tema.Bolha2);

            // Ondas suaves
            using (var p = new Pen(Visual.Alfa(tema.Destaque, 38), 2f * f))
                g.DrawBezier(p, 0, tam.Height * 0.70f, tam.Width * 0.3f, tam.Height * 0.62f, tam.Width * 0.6f, tam.Height * 0.86f, tam.Width, tam.Height * 0.70f);
            using (var p = new Pen(Visual.Alfa(tema.Destaque, 24), 2f * f))
                g.DrawBezier(p, 0, tam.Height * 0.76f, tam.Width * 0.35f, tam.Height * 0.68f, tam.Width * 0.6f, tam.Height * 0.93f, tam.Width, tam.Height * 0.78f);

            PainelVidro(g, S(AreaCabecalho), 18 * f, f);
            PainelVidro(g, S(AreaCartao), 20 * f, f);

            // Logo: quadrado em degradê com ícone de documento
            var rl = S(AreaLogo);
            Visual.Sombra(g, rl, 14 * f, Visual.Alfa(tema.LogoB, 90), 6, 3 * f);
            using (var path = Visual.Arredondado(rl, 13 * f))
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(rl, tema.LogoA, tema.LogoB, 45f))
                g.FillPath(br, path);
            using (var fonte = Visual.FonteIcone(17f))
                Visual.DesenharIcone(g, Visual.IconeDocumento, fonte, Color.White, rl);

            // Ícones das seções e dos rótulos
            using (var fonteSecao = Visual.FonteIcone(10.5f))
                foreach (var (area, icone) in iconesSecao)
                {
                    var r = S(area);
                    using var path = Visual.Arredondado(r, 8 * f);
                    using var br = new System.Drawing.Drawing2D.LinearGradientBrush(r, tema.BadgeA, tema.BadgeB, 45f);
                    g.FillPath(br, path);
                    Visual.DesenharIcone(g, icone, fonteSecao, tema.Secao, r);
                }
            using (var fonteRotulo = Visual.FonteIcone(8.5f))
                foreach (var (area, icone) in iconesRotulo)
                    Visual.DesenharIcone(g, icone, fonteRotulo, tema.IconeRotulo, S(area));

            // Sombra do botão principal
            if (btnGerar != null)
            {
                var rb = new RectangleF(btnGerar.Left + 6 * f, btnGerar.Top + 4 * f, btnGerar.Width - 12 * f, btnGerar.Height - 4 * f);
                Visual.Sombra(g, rb, 13 * f, Visual.Alfa(tema.BotaoB, isDarkMode ? 70 : 80), 8, 4 * f);
            }
            return bmp;
        }

        private static void Bolha(Graphics g, RectangleF r, Color cor)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(r);
            using var br = new System.Drawing.Drawing2D.PathGradientBrush(path) {
                CenterColor = cor, SurroundColors = new[] { Color.FromArgb(0, cor) }
            };
            g.FillPath(br, path);
        }

        private void PainelVidro(Graphics g, RectangleF r, float raio, float f)
        {
            Visual.Sombra(g, r, raio, tema.Sombra, 10, 4 * f);
            using var path = Visual.Arredondado(r, raio);
            using (var b = new SolidBrush(tema.Vidro)) g.FillPath(b, path);
            using (var p = new Pen(tema.VidroBorda, 1f * f)) g.DrawPath(p, path);
        }

        // =====================================================================
        //  Tema
        // =====================================================================

        internal sealed class Tema
        {
            public Color Fundo, FundoTopo, FundoBase, Bolha1, Bolha2, Vidro, VidroBorda, Sombra,
                Campo, Borda, Texto, Rotulo, Mutado, Destaque, Secao, BadgeA, BadgeB, IconeRotulo,
                LogoA, LogoB, BotaoA, BotaoB, BotaoSecundario, BotaoSecundarioBorda, BotaoTema, IconeTema,
                Erro, Ok, Aviso;

            public static readonly Tema Escuro = new Tema {
                Fundo = Color.FromArgb(14, 24, 37), FundoTopo = Color.FromArgb(13, 23, 36), FundoBase = Color.FromArgb(8, 17, 27),
                Bolha1 = Color.FromArgb(70, 0, 140, 255), Bolha2 = Color.FromArgb(56, 0, 170, 220),
                Vidro = Color.FromArgb(190, 22, 36, 54), VidroBorda = Color.FromArgb(46, 120, 170, 230), Sombra = Color.FromArgb(90, 0, 0, 0),
                Campo = Color.FromArgb(12, 22, 35), Borda = Color.FromArgb(48, 70, 98), Texto = Color.FromArgb(231, 240, 250),
                Rotulo = Color.FromArgb(213, 227, 242), Mutado = Color.FromArgb(124, 147, 173), Destaque = Color.FromArgb(43, 155, 255),
                Secao = Color.FromArgb(108, 192, 255), BadgeA = Color.FromArgb(28, 58, 92), BadgeB = Color.FromArgb(20, 44, 72),
                IconeRotulo = Color.FromArgb(127, 163, 200),
                LogoA = Color.FromArgb(31, 182, 240), LogoB = Color.FromArgb(10, 99, 214),
                BotaoA = Color.FromArgb(43, 155, 255), BotaoB = Color.FromArgb(10, 98, 216),
                BotaoSecundario = Color.FromArgb(24, 38, 56), BotaoSecundarioBorda = Color.FromArgb(70, 140, 205),
                BotaoTema = Color.FromArgb(30, 46, 66), IconeTema = Color.FromArgb(255, 211, 107),
                Erro = Color.FromArgb(240, 95, 85), Ok = Color.FromArgb(110, 220, 150), Aviso = Color.FromArgb(235, 175, 60)
            };
            public static readonly Tema Claro = new Tema {
                Fundo = Color.FromArgb(232, 243, 252), FundoTopo = Color.FromArgb(234, 245, 255), FundoBase = Color.FromArgb(195, 227, 248),
                Bolha1 = Color.FromArgb(140, 120, 210, 255), Bolha2 = Color.FromArgb(115, 80, 190, 250),
                Vidro = Color.FromArgb(165, 255, 255, 255), VidroBorda = Color.FromArgb(235, 255, 255, 255), Sombra = Color.FromArgb(46, 40, 110, 180),
                Campo = Color.FromArgb(251, 253, 255), Borda = Color.FromArgb(205, 222, 239), Texto = Color.FromArgb(14, 44, 75),
                Rotulo = Color.FromArgb(18, 53, 90), Mutado = Color.FromArgb(90, 117, 147), Destaque = Color.FromArgb(10, 99, 214),
                Secao = Color.FromArgb(10, 91, 196), BadgeA = Color.FromArgb(228, 243, 255), BadgeB = Color.FromArgb(207, 230, 251),
                IconeRotulo = Color.FromArgb(75, 120, 168),
                LogoA = Color.FromArgb(31, 182, 240), LogoB = Color.FromArgb(10, 99, 214),
                BotaoA = Color.FromArgb(26, 138, 240), BotaoB = Color.FromArgb(6, 86, 201),
                BotaoSecundario = Color.FromArgb(246, 251, 255), BotaoSecundarioBorda = Color.FromArgb(143, 196, 242),
                BotaoTema = Color.FromArgb(250, 253, 255), IconeTema = Color.FromArgb(10, 99, 214),
                Erro = Color.FromArgb(200, 45, 35), Ok = Color.FromArgb(25, 135, 65), Aviso = Color.FromArgb(170, 100, 0)
            };
        }

        private void AplicarTema()
        {
            tema = isDarkMode ? Tema.Escuro : Tema.Claro;
            fundoCache?.Dispose(); fundoCache = null;

            BackColor = tema.Fundo;
            btnAlternarTema.Icone = isDarkMode ? Visual.IconeSol : Visual.IconeLua;
            btnAlternarTema.CorFundo = tema.BotaoTema;
            btnAlternarTema.CorBorda = tema.VidroBorda;
            btnAlternarTema.CorTexto = tema.IconeTema;

            foreach (Control c in Controls)
            {
                if (c is CaixaCampo caixa)
                {
                    caixa.CorFundo = tema.Campo; caixa.CorBorda = tema.Borda; caixa.CorFoco = tema.Destaque; caixa.CorErro = tema.Erro;
                    foreach (Control filho in caixa.Controls)
                    {
                        filho.BackColor = tema.Campo; filho.ForeColor = tema.Texto;
                        if (filho is ComboTema ct) { ct.CorSeta = tema.IconeRotulo; ct.CorSelecao = tema.Destaque; ct.Invalidate(); }
                    }
                }
                else if (c is Label lbl && !(c is LinkLabel) && !(c is RotuloStatus))
                {
                    string? tag = lbl.Tag as string;
                    lbl.ForeColor = tag == "secao" ? tema.Secao : tag == "mutado" ? tema.Mutado : tag == "aviso" ? tema.Aviso
                        : lbl == lblTituloApp ? tema.Texto : tema.Rotulo;
                }
            }

            lstSugestoes.BackColor = tema.Campo;
            lstSugestoes.ForeColor = tema.Texto;

            btnGerar.CorA = tema.BotaoA; btnGerar.CorB = tema.BotaoB; btnGerar.CorTexto = Color.White;
            btnLimpar.CorFundo = tema.BotaoSecundario; btnLimpar.CorBorda = tema.BotaoSecundarioBorda; btnLimpar.CorTexto = tema.Destaque;

            foreach (var lnk in Controls.OfType<LinkLabel>())
            {
                lnk.LinkColor = tema.Destaque;
                lnk.ActiveLinkColor = tema.Destaque;
                lnk.VisitedLinkColor = tema.Destaque;
                lnk.DisabledLinkColor = tema.Mutado;
            }

            AplicarBarraDeTitulo();
            Invalidate(true);

            // Barra de rolagem escura na justificativa e na lista de sugestões
            foreach (Control c in new Control[] { txtObs, lstSugestoes })
                if (c.IsHandleCreated) { try { SetWindowTheme(c.Handle, isDarkMode ? "DarkMode_Explorer" : "Explorer", null); } catch (Exception) { } }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Deixa a barra de título escura no tema escuro (Windows 10 20H1+ / Windows 11)
        private void AplicarBarraDeTitulo()
        {
            if (!IsHandleCreated) return;
            int valor = isDarkMode ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(Handle, 20, ref valor, sizeof(int)) != 0)
                    DwmSetWindowAttribute(Handle, 19, ref valor, sizeof(int));
            }
            catch (Exception) { /* versões antigas do Windows: ignora */ }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AplicarBarraDeTitulo();
        }

        // Preferências simples "chave=valor" (tema, último profissional e pasta dos laudos)
        private static string? LerPreferencia(string chave)
        {
            try
            {
                if (!File.Exists(ArquivoPreferencias)) return null;
                string? linha = File.ReadLines(ArquivoPreferencias).FirstOrDefault(l => l.StartsWith(chave + "="));
                return linha?.Substring(chave.Length + 1).Trim();
            }
            catch (Exception) { return null; }
        }

        private static bool CarregarTemaPreferido()
        {
            string? salvo = LerPreferencia("tema");
            if (salvo != null) return salvo == "escuro";
            try
            {
                // Sem preferência salva: segue o tema do Windows
                object? claro = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                return claro is int v && v == 0;
            }
            catch (Exception) { return true; }
        }

        private bool temaEscolhido = LerPreferencia("tema") != null;

        private void SalvarPreferencias()
        {
            if (!inicializado) return;
            try
            {
                var linhas = new List<string>();
                if (temaEscolhido) linhas.Add("tema=" + (isDarkMode ? "escuro" : "claro"));
                if (cbProfissional.SelectedItem is Profissional p) linhas.Add("profissional=" + p.Cns);
                if (pastaLaudosEscolhida != null) linhas.Add("pastaLaudos=" + pastaLaudosEscolhida);
                Directory.CreateDirectory(Path.GetDirectoryName(ArquivoPreferencias)!);
                File.WriteAllLines(ArquivoPreferencias, linhas);
            }
            catch (Exception) { /* preferência é opcional */ }
        }

        // =====================================================================
        //  Profissionais
        // =====================================================================

        private void PreencherProfissionais(string? cnsSelecionado)
        {
            cbProfissional.BeginUpdate();
            cbProfissional.Items.Clear();
            foreach (var p in profissionais.OrderBy(p => p.Nome)) cbProfissional.Items.Add(p);
            cbProfissional.EndUpdate();

            int indice = cbProfissional.Items.Cast<Profissional>().ToList().FindIndex(p => p.Cns == cnsSelecionado);
            cbProfissional.SelectedIndex = indice >= 0 ? indice : (cbProfissional.Items.Count > 0 ? 0 : -1);
            AtualizarAcoesProfissional();
        }

        private void AtualizarAcoesProfissional()
        {
            bool temSelecionado = cbProfissional.SelectedItem is Profissional;
            lnkEditarProf.Enabled = temSelecionado;
            lnkExcluirProf.Enabled = temSelecionado;
        }

        private bool PersistirProfissionais()
        {
            try { RepositorioProfissionais.Salvar(profissionais); return true; }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível salvar o cadastro de profissionais:\n\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void CadastrarProfissional()
        {
            using var dlg = new FormProfissional(tema, profissionais);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Resultado == null) return;
            profissionais.Add(dlg.Resultado);
            if (!PersistirProfissionais()) { profissionais.Remove(dlg.Resultado); return; }
            PreencherProfissionais(dlg.Resultado.Cns);
            MostrarStatus("✓ Profissional cadastrado: " + dlg.Resultado.Nome, tema.Ok);
        }

        private void EditarProfissional()
        {
            if (cbProfissional.SelectedItem is not Profissional atual) return;
            using var dlg = new FormProfissional(tema, profissionais.Where(p => p != atual).ToList(), atual);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Resultado == null) return;
            string nomeAntigo = atual.Nome, cnsAntigo = atual.Cns;
            atual.Nome = dlg.Resultado.Nome;
            atual.Cns = dlg.Resultado.Cns;
            if (!PersistirProfissionais()) { atual.Nome = nomeAntigo; atual.Cns = cnsAntigo; return; }
            PreencherProfissionais(atual.Cns);
            MostrarStatus("✓ Profissional atualizado.", tema.Ok);
        }

        private void ExcluirProfissional()
        {
            if (cbProfissional.SelectedItem is not Profissional atual) return;
            if (MessageBox.Show($"Excluir {atual.Nome} da lista de profissionais?", "Excluir profissional",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            int pos = profissionais.IndexOf(atual);
            profissionais.Remove(atual);
            if (!PersistirProfissionais()) { profissionais.Insert(pos, atual); return; }
            PreencherProfissionais(null);
            MostrarStatus("Profissional excluído.", tema.Texto);
        }

        // =====================================================================
        //  Teclado
        // =====================================================================

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Enter)) { btnGerar.PerformClick(); return true; }
            if (keyData == (Keys.Control | Keys.N)) { NovoLaudoMesmoPaciente(); return true; }

            if (lstSugestoes.Visible)
            {
                switch (keyData)
                {
                    case Keys.Down:
                        lstSugestoes.SelectedIndex = Math.Min(lstSugestoes.SelectedIndex + 1, lstSugestoes.Items.Count - 1);
                        return true;
                    case Keys.Up:
                        lstSugestoes.SelectedIndex = Math.Max(lstSugestoes.SelectedIndex - 1, 0);
                        return true;
                    case Keys.Enter:
                        if (lstSugestoes.SelectedIndex >= 0) { SelecionarSugestao(lstSugestoes.SelectedIndex); return true; }
                        break;
                    case Keys.Escape:
                        OcultarSugestoes();
                        return true;
                }
            }

            if (keyData == Keys.Enter)
            {
                Control? ativo = ActiveControl;
                bool multilinha = ativo is TextBox tb && tb.Multiline;
                bool comboAberto = ativo is ComboBox cb && cb.DroppedDown;
                if (!multilinha && !comboAberto && !(ativo is Button) && !(ativo is LinkLabel))
                {
                    if (ativo == txtCid) RealizarBuscaCid();
                    SelectNextControl(ativo, true, true, true, true);
                    // Enter percorre só os campos; os links continuam acessíveis pelo Tab
                    for (int i = 0; i < 5 && ActiveControl is LinkLabel; i++)
                        SelectNextControl(ActiveControl, true, true, true, true);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // =====================================================================
        //  CID: busca por código ou nome
        // =====================================================================

        private void CarregarCids()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cids.txt");
            try
            {
                if (!File.Exists(path)) return;
                foreach (string linha in File.ReadLines(path))
                {
                    string[] p = linha.Split(',', 2);
                    if (p.Length != 2 || string.IsNullOrWhiteSpace(p[0])) continue;
                    string codigo = p[0].Trim().ToUpperInvariant();
                    string descricao = p[1].Trim().ToUpperInvariant();
                    cids.Add((codigo, descricao, Normalizar(codigo + " " + descricao), codigo.Replace(".", ""), Normalizar(descricao)));
                }
            }
            catch (IOException) { /* sem lista: a busca simplesmente não sugere nada */ }
        }

        // Maiúsculas e sem acentos, para "hipertensao" encontrar "HIPERTENSÃO"
        private static string Normalizar(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString().ToUpperInvariant();
        }

        private void ConfigurarBuscaCid(TextBox origem)
        {
            origem.TextChanged += (s, e) => {
                if (preenchendoCid || ActiveControl != origem) return;
                if (origem == txtCid) lblAvisoCid.Visible = false;
                MostrarSugestoes(origem);
            };
            origem.Leave += (s, e) => BeginInvoke(new Action(() => {
                if (lstSugestoes.ContainsFocus) return;   // clicou numa sugestão
                OcultarSugestoes();
                if (origem == txtCid) RealizarBuscaCid();
            }));
        }

        private void MostrarSugestoes(TextBox origem)
        {
            string termo = Normalizar(origem.Text.Trim());
            if (termo.Length < (origem == txtCid ? 1 : 3)) { OcultarSugestoes(); return; }

            string termoCodigo = termo.Replace(".", "");
            var resultado = cids
                .Select(c => new {
                    c,
                    prioridade = c.CodigoSemPonto.StartsWith(termoCodigo, StringComparison.Ordinal) ? 0
                               : c.DescricaoNorm.StartsWith(termo, StringComparison.Ordinal) ? 1
                               : c.Busca.Contains(" " + termo, StringComparison.Ordinal) ? 2   // início de palavra
                               : c.Busca.Contains(termo, StringComparison.Ordinal) ? 3 : -1
                })
                .Where(x => x.prioridade >= 0)
                // Código/início do nome: categorias (3 caracteres) antes das subcategorias.
                // Palavra no meio do nome: descrições mais curtas (mais diretas) primeiro.
                .OrderBy(x => x.prioridade)
                .ThenBy(x => x.prioridade <= 1 ? x.c.Codigo.Length : x.c.Descricao.Length)
                .ThenBy(x => x.c.Codigo, StringComparer.Ordinal)
                .Take(8)
                .ToList();

            // CID exato digitado: não precisa da lista
            if (resultado.Count == 0 || (origem == txtCid && resultado.Count == 1 && resultado[0].c.CodigoSemPonto == termoCodigo))
            {
                OcultarSugestoes();
                return;
            }

            lstSugestoes.BeginUpdate();
            lstSugestoes.Items.Clear();
            foreach (var r in resultado) lstSugestoes.Items.Add($"{r.c.Codigo}  —  {r.c.Descricao}");
            lstSugestoes.Tag = resultado.Select(r => r.c).ToList();
            lstSugestoes.SelectedIndex = 0;
            lstSugestoes.EndUpdate();

            var caixaCid = caixas[txtCid];
            var caixaOrigem = caixas[origem];
            lstSugestoes.Location = new Point(caixaCid.Left, caixaOrigem.Bottom + 2);
            lstSugestoes.Size = new Size(LarguraUtil, lstSugestoes.ItemHeight * resultado.Count + 4);
            lstSugestoes.Visible = true;
            lstSugestoes.BringToFront();
        }

        private void OcultarSugestoes()
        {
            lstSugestoes.Visible = false;
        }

        private void SelecionarSugestao(int indice)
        {
            if (lstSugestoes.Tag is not List<(string Codigo, string Descricao, string Busca, string CodigoSemPonto, string DescricaoNorm)> lista || indice >= lista.Count) return;
            var escolhido = lista[indice];

            preenchendoCid = true;
            txtCid.Text = escolhido.Codigo;
            txtDiagnostico.Text = escolhido.Descricao;
            preenchendoCid = false;
            diagnosticoAutomatico = escolhido.Descricao;
            lblAvisoCid.Visible = false;
            caixas[txtCid].Erro = false;

            OcultarSugestoes();
            txtObs.Select();
        }

        // Aceita o código com ou sem ponto (M545 ou M54.5); devolve o código no formato oficial
        private (string Codigo, string Descricao)? BuscarCid(string cid)
        {
            string alvo = cid.Trim().ToUpperInvariant().Replace(".", "");
            foreach (var c in cids) if (c.CodigoSemPonto == alvo) return (c.Codigo, c.Descricao);
            return null;
        }

        // Ao sair do campo CID: preenche o diagnóstico se o código estiver na lista
        private void RealizarBuscaCid()
        {
            string cid = txtCid.Text.Trim().ToUpperInvariant();
            var encontrado = cid == "" ? null : BuscarCid(cid);

            if (encontrado is var (codigoOficial, descricao))
            {
                preenchendoCid = true;
                if (txtCid.Text != codigoOficial) txtCid.Text = codigoOficial;
                txtDiagnostico.Text = descricao;
                preenchendoCid = false;
                diagnosticoAutomatico = descricao;
                lblAvisoCid.Visible = false;
                return;
            }

            if (diagnosticoAutomatico != null && txtDiagnostico.Text == diagnosticoAutomatico)
            {
                // CID trocado/apagado: remove o diagnóstico que veio do CID anterior.
                // Um diagnóstico digitado à mão é mantido.
                preenchendoCid = true;
                txtDiagnostico.Text = "";
                preenchendoCid = false;
                diagnosticoAutomatico = null;
            }

            lblAvisoCid.Text = "CID não encontrado na tabela CID-10 — confira o código ou preencha o diagnóstico.";
            lblAvisoCid.Visible = cid != "";
        }

        // =====================================================================
        //  Data
        // =====================================================================

        // Mantém o formato dd/MM/aaaa a partir dos dígitos digitados.
        // As barras são recalculadas a cada mudança, então o Backspace funciona normalmente.
        private void FormatarData(TextBox tb)
        {
            if (formatandoData) return;

            int digitosAntesDoCursor = tb.Text.Substring(0, Math.Min(tb.SelectionStart, tb.Text.Length)).Count(char.IsDigit);
            string digitos = new string(tb.Text.Where(char.IsDigit).Take(8).ToArray());
            string formatado = digitos.Length switch {
                > 4 => $"{digitos[..2]}/{digitos[2..4]}/{digitos[4..]}",
                > 2 => $"{digitos[..2]}/{digitos[2..]}",
                _ => digitos
            };
            if (formatado == tb.Text) return;

            int cursor = 0, vistos = 0;
            while (cursor < formatado.Length && vistos < digitosAntesDoCursor)
            {
                if (char.IsDigit(formatado[cursor])) vistos++;
                cursor++;
            }

            formatandoData = true;
            tb.Text = formatado;
            tb.SelectionStart = cursor;
            formatandoData = false;
        }

        private static bool DataValida(string data, out DateTime dt) =>
            DateTime.TryParseExact(data, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
            && dt <= DateTime.Today && dt.Year >= 1900;

        // =====================================================================
        //  Ações
        // =====================================================================

        private void MostrarStatus(string texto, Color cor)
        {
            lblStatus.Text = texto;
            lblStatus.ForeColor = cor;
        }

        private bool AlgumCampoPreenchido() =>
            new[] { txtNome, txtDataNasc, txtMae, txtProcedimento, txtCid, txtDiagnostico, txtObs }.Any(t => t.Text.Trim() != "")
            || cbSexo.SelectedIndex >= 0 || cbRaca.SelectedIndex >= 0;

        private void BtnLimpar_Click(object? sender, EventArgs e)
        {
            // Só pergunta se há dados que ainda não viraram laudo
            if (AlgumCampoPreenchido() && alteradoDesdeUltimoLaudo &&
                MessageBox.Show("Apagar todos os campos preenchidos?", "Limpar formulário",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            limpandoFormulario = true;
            txtNome.Clear(); txtDataNasc.Clear(); txtMae.Clear();
            cbSexo.SelectedIndex = -1; cbRaca.SelectedIndex = -1;
            limpandoFormulario = false;
            LimparDadosClinicos();
            MostrarStatus("", tema.Texto);
            txtNome.Select();
        }

        private void LimparDadosClinicos()
        {
            limpandoFormulario = true;
            preenchendoCid = true;
            foreach (var t in new[] { txtProcedimento, txtCid, txtDiagnostico, txtObs }) t.Clear();
            preenchendoCid = false;
            cbQuantidade.SelectedIndex = 0;
            limpandoFormulario = false;
            diagnosticoAutomatico = null;
            foreach (var caixa in caixas.Values) caixa.Erro = false;
            lblAvisoCid.Visible = false;
            OcultarSugestoes();
            alteradoDesdeUltimoLaudo = false;
        }

        // Mantém profissional e paciente; limpa só o que muda de um laudo para outro
        private void NovoLaudoMesmoPaciente()
        {
            bool clinicoPreenchido = new[] { txtProcedimento, txtCid, txtDiagnostico, txtObs }.Any(t => t.Text.Trim() != "");
            if (clinicoPreenchido && alteradoDesdeUltimoLaudo &&
                MessageBox.Show("Os dados clínicos atuais ainda não foram gerados em laudo.\n\nApagar procedimento, CID e justificativa mesmo assim?",
                    "Novo laudo, mesmo paciente", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            bool pacienteAlterado = alteradoDesdeUltimoLaudo && !clinicoPreenchido;
            LimparDadosClinicos();
            alteradoDesdeUltimoLaudo = pacienteAlterado;
            MostrarStatus(txtNome.Text.Trim() == "" ? "" : "Novo laudo para " + txtNome.Text.Trim() + ".", tema.Texto);
            txtProcedimento.Select();
        }

        private void BtnGerar_Click(object? sender, EventArgs e)
        {
            OcultarSugestoes();
            if (ActiveControl == txtCid || ActiveControl == btnGerar) RealizarBuscaCid();

            // Nome é obrigatório (usado no nome do arquivo)
            if (txtNome.Text.Trim() == "")
            {
                caixas[txtNome].Erro = true;
                MostrarStatus("Informe o nome do paciente.", tema.Erro);
                txtNome.Select();
                return;
            }

            if (cbProfissional.SelectedItem is not Profissional profissional)
            {
                caixas[cbProfissional].Erro = true;
                MostrarStatus("Cadastre ou escolha o(a) médico(a) solicitante.", tema.Erro);
                cbProfissional.Select();
                return;
            }

            string dataNasc = txtDataNasc.Text.Trim();
            if (dataNasc != "" && !DataValida(dataNasc, out _))
            {
                caixas[txtDataNasc].Erro = true;
                MostrarStatus("Data de nascimento inválida. Use o formato dd/mm/aaaa.", tema.Erro);
                txtDataNasc.Select();
                return;
            }

            // Demais obrigatórios: destaca e pergunta se quer gerar mesmo assim
            var faltando = new List<(Control Campo, string Nome)>();
            if (dataNasc == "") faltando.Add((txtDataNasc, "Nascimento"));
            if (cbSexo.SelectedIndex < 0) faltando.Add((cbSexo, "Sexo"));
            if (txtMae.Text.Trim() == "") faltando.Add((txtMae, "Nome da mãe"));
            if (txtProcedimento.Text.Trim() == "") faltando.Add((txtProcedimento, "Procedimento"));
            if (txtCid.Text.Trim() == "") faltando.Add((txtCid, "CID-10"));
            foreach (var f in faltando) caixas[f.Campo].Erro = true;

            if (faltando.Count > 0)
            {
                MostrarStatus("Campos obrigatórios em branco: " + string.Join(", ", faltando.Select(f => f.Nome)) + ".", tema.Erro);
                var resp = MessageBox.Show(
                    "Os seguintes campos obrigatórios estão em branco:\n\n• " + string.Join("\n• ", faltando.Select(f => f.Nome)) +
                    "\n\nGerar o laudo mesmo assim?",
                    "Campos em branco", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (resp != DialogResult.Yes) { faltando[0].Campo.Select(); return; }
            }

            var pac = new DadosApac {
                NomePaciente = txtNome.Text.Trim().ToUpper(),
                DataNascimento = dataNasc,
                Sexo = cbSexo.Text,
                NomeMae = txtMae.Text.Trim().ToUpper(),
                ProcedimentoPrincipal = txtProcedimento.Text.Trim().ToUpper(),
                DescricaoDiagnostico = txtDiagnostico.Text.Trim().ToUpper(),
                Cid10Principal = txtCid.Text.Trim().ToUpper(),
                Observacoes = txtObs.Text.Trim().ToUpper(),
                Quantidade = cbQuantidade.Text,
                RacaCor = cbRaca.Text,
                NomeProfissional = profissional.Nome,
                CnsProfissional = profissional.Cns
            };

            string pasta = AppDomain.CurrentDomain.BaseDirectory;
            string nomeArquivo = CriarNomeArquivoSeguro(pac.NomePaciente);
            // Data e hora no nome evitam sobrescrever laudos anteriores (ou um PDF aberto no leitor)
            string outPath = Path.Combine(PastaLaudos, $"Laudo_{nomeArquivo}_{DateTime.Now:yyyy-MM-dd_HHmmss}.pdf");
            List<string> avisos;
            try
            {
                Cursor = Cursors.WaitCursor;
                avisos = new GeradorPdfService().PreencherLaudo(pac, Path.Combine(pasta, "Templates", "Laudo de APAC - Modelo.pdf"), outPath);
            }
            catch (Exception ex)
            {
                MostrarStatus("Não foi possível gerar o laudo.", tema.Erro);
                MessageBox.Show("Erro ao gerar o laudo:\n\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally { Cursor = Cursors.Default; }

            alteradoDesdeUltimoLaudo = false;
            MostrarStatus("✓ Laudo salvo: " + Path.GetFileName(outPath), tema.Ok);
            try
            {
                if (AbrirPdfAoGerar) Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // O laudo foi salvo; só não há leitor de PDF disponível para abri-lo
                MessageBox.Show("O laudo foi salvo, mas não foi possível abri-lo automaticamente:\n\n" + ex.Message +
                    "\n\nUse \"Abrir pasta dos laudos\" para encontrá-lo.", "Laudo salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (avisos.Count > 0)
                MessageBox.Show(string.Join("\n\n", avisos), "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void AbrirPastaLaudos()
        {
            try
            {
                Directory.CreateDirectory(PastaLaudos);
                Process.Start(new ProcessStartInfo(PastaLaudos) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Não foi possível abrir a pasta:\n\n" + ex.Message); }
        }

        private static string CriarNomeArquivoSeguro(string? nomePaciente)
        {
            string nome = string.IsNullOrWhiteSpace(nomePaciente) ? "Paciente" : nomePaciente.Replace(" ", "_");
            foreach (char caractereInvalido in Path.GetInvalidFileNameChars())
                nome = nome.Replace(caractereInvalido, '_');
            return nome;
        }

        // Versões anteriores salvavam os laudos em "Saida", dentro da pasta do programa,
        // que é apagada ao desinstalar. Move esses arquivos para a pasta de Documentos.
        private static void MigrarLaudosAntigos()
        {
            string pastaAntiga = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Saida");
            try
            {
                if (!Directory.Exists(pastaAntiga)) return;
                Directory.CreateDirectory(PastaLaudos);
                foreach (string arquivo in Directory.GetFiles(pastaAntiga, "*.pdf"))
                {
                    string destino = Path.Combine(PastaLaudos, Path.GetFileName(arquivo));
                    if (!File.Exists(destino)) File.Move(arquivo, destino);
                }
                if (!Directory.EnumerateFileSystemEntries(pastaAntiga).Any()) Directory.Delete(pastaAntiga);
            }
            catch (Exception)
            {
                // Migração é opcional: se falhar, os arquivos continuam onde estavam.
            }
        }

        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormPrincipal());
        }
    }

    /// <summary>
    /// ComboBox que respeita o tema: esconde a borda/seta clara do estilo Flat
    /// (a borda vem da CaixaCampo) e desenha os itens com as cores do tema.
    /// </summary>
    internal sealed class ComboTema : ComboBox
    {
        public Color CorSeta = Color.Gray, CorSelecao = Color.DodgerBlue;
        private const int WM_PAINT = 0x000F;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WM_PAINT) return;

            using var g = Graphics.FromHwnd(Handle);
            using var fundo = new Pen(BackColor, 2);
            g.DrawRectangle(fundo, 0, 0, Width - 1, Height - 1);   // apaga a borda do estilo Flat

            int larguraSeta = SystemInformation.VerticalScrollBarWidth;
            var areaSeta = new Rectangle(Width - larguraSeta - 1, 1, larguraSeta, Height - 2);
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, areaSeta);

            // Chevron
            int cx = areaSeta.Left + areaSeta.Width / 2, cy = Height / 2;
            using var pen = new Pen(CorSeta, 1.6f);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) { using var b0 = new SolidBrush(BackColor); e.Graphics.FillRectangle(b0, e.Bounds); return; }
            bool editando = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool selecionado = (e.State & DrawItemState.Selected) != 0 && !editando;
            using (var b = new SolidBrush(selecionado ? CorSelecao : BackColor)) e.Graphics.FillRectangle(b, e.Bounds);
            var cor = selecionado ? Color.White : ForeColor;
            var r = new Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 5, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, Items[e.Index]?.ToString(), Font, r, cor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    /// <summary>
    /// Caixa arredondada em volta de um campo: borda que muda de cor no foco (com halo) e em caso de erro.
    /// Os cantos mostram o fundo do formulário.
    /// </summary>
    internal sealed class CaixaCampo : Panel
    {
        private bool focado, erro;
        public Color CorFundo = Color.White, CorBorda = Color.Gray, CorFoco = Color.DodgerBlue, CorErro = Color.Red;
        public const float Raio = 10f;

        public CaixaCampo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Focado { get => focado; set { focado = value; Invalidate(); } }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Erro { get => erro; set { if (erro != value) { erro = value; Invalidate(); } } }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Visual.PintarFundoDoPai(this, g);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color cor = erro ? CorErro : focado ? CorFoco : CorBorda;
            if (erro || focado)
            {
                // Halo suave em volta do campo ativo
                using var halo = Visual.Arredondado(new RectangleF(1, 1, Width - 3, Height - 3), Raio + 1.5f);
                using var penHalo = new Pen(Visual.Alfa(cor, 55), 3f);
                g.DrawPath(penHalo, halo);
            }
            var r = new RectangleF(2.5f, 2.5f, Width - 6f, Height - 6f);
            using var path = Visual.Arredondado(r, Raio);
            using (var b = new SolidBrush(CorFundo)) g.FillPath(b, path);
            using var pen = new Pen(cor, (erro || focado) ? 1.6f : 1f);
            g.DrawPath(pen, path);
        }
    }
}
