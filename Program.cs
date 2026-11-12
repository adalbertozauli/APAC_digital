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
    public class FormPrincipal : Form
    {
        // ---------- Controles ----------
        private TextBox txtNome = null!, txtDataNasc = null!, txtMae = null!, txtCid = null!, txtDiagnostico = null!, txtProcedimento = null!, txtObs = null!;
        private ComboBox cbSexo = null!, cbRaca = null!, cbQuantidade = null!, cbProfissional = null!;
        private LinkLabel lnkNovoProf = null!, lnkEditarProf = null!, lnkExcluirProf = null!;
        private List<Profissional> profissionais = new List<Profissional>();
        private Button btnGerar = null!, btnLimpar = null!, btnAlternarTema = null!;
        private Panel headerPanel = null!;
        private Label lblTituloApp = null!, lblAvisoCid = null!, lblStatus = null!, lblAtalhos = null!;
        private LinkLabel lnkPasta = null!, lnkMesmoPaciente = null!;
        private ListBox lstSugestoes = null!;
        private readonly ToolTip dicas = new ToolTip();

        // Cada campo fica dentro de uma "caixa" com borda (foco/erro)
        private readonly Dictionary<Control, CaixaCampo> caixas = new Dictionary<Control, CaixaCampo>();
        private readonly List<Label> labelsSecao = new List<Label>();
        private readonly List<Panel> divisores = new List<Panel>();

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

        internal static string PastaLaudos { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "APAC Digital", "Laudos");

        internal static bool AbrirPdfAoGerar { get; set; } = true;

        private static readonly string ArquivoPreferencias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "APAC Digital", "preferencias.txt");

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
            Shown += (s, e) => { AplicarTema(); txtNome.Focus(); };
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
            // --- Cabeçalho ---
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 60 };
            lblTituloApp = new Label {
                Text = "Emissão de Laudo APAC",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(Margem - 2, 15)
            };
            btnAlternarTema = new Button {
                Size = new Size(40, 32),
                Location = new Point(Margem + LarguraUtil - 40, 14),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Symbol", 13F),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnAlternarTema.FlatAppearance.BorderSize = 0;
            btnAlternarTema.Click += (s, e) => { isDarkMode = !isDarkMode; temaEscolhido = true; SalvarPreferencias(); AplicarTema(); };
            dicas.SetToolTip(btnAlternarTema, "Alternar tema claro / escuro");
            headerPanel.Controls.Add(lblTituloApp);
            headerPanel.Controls.Add(btnAlternarTema);
            Controls.Add(headerPanel);

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

            int y = 76;
            CriarSecao("PROFISSIONAL SOLICITANTE", ref y);

            cbProfissional = CriarCombo("Médico(a) solicitante *", Margem, y, LarguraUtil, Array.Empty<string>());
            cbProfissional.SelectedIndexChanged += (s, e) => { AtualizarAcoesProfissional(); SalvarPreferencias(); };
            // Links na linha do rótulo, alinhados à direita (criados da esquerda p/ direita para a ordem do Tab)
            var fonteLink = new Font("Segoe UI", 8.5F);
            string[] textosLinks = { "+ Cadastrar novo", "Editar", "Excluir" };
            int xLink = Margem + LarguraUtil - textosLinks.Sum(t => TextRenderer.MeasureText(t, fonteLink).Width) - 10 * (textosLinks.Length - 1);
            lnkNovoProf = CriarLinkAcao(textosLinks[0], ref xLink, y, CadastrarProfissional);
            lnkEditarProf = CriarLinkAcao(textosLinks[1], ref xLink, y, EditarProfissional);
            lnkExcluirProf = CriarLinkAcao(textosLinks[2], ref xLink, y, ExcluirProfissional);
            PreencherProfissionais(LerPreferencia("profissional"));
            y += 72;

            CriarSecao("DADOS DO PACIENTE", ref y);

            txtNome = CriarTexto("Nome completo *", Margem, y, LarguraUtil, "Ex: JOÃO DA SILVA");
            y += 62;

            txtDataNasc = CriarTexto("Nascimento *", Margem, y, 130, "dd/mm/aaaa");
            txtDataNasc.MaxLength = 10;
            txtDataNasc.CharacterCasing = CharacterCasing.Normal;
            txtDataNasc.TextChanged += (s, e) => FormatarData(txtDataNasc);
            cbSexo = CriarCombo("Sexo *", 185, y, 80, new[] { "M", "F" });
            cbRaca = CriarCombo("Raça/Cor", 290, y, 205, new[] { "BRANCA", "PRETA", "PARDA", "AMARELA", "INDÍGENA" });
            y += 62;

            txtMae = CriarTexto("Nome da mãe *", Margem, y, LarguraUtil, "Ex: MARIA DA SILVA");
            y += 72;

            CriarSecao("INFORMAÇÕES CLÍNICAS", ref y);

            txtProcedimento = CriarTexto("Procedimento principal *", Margem, y, 380, "Ex: TC DE CRÂNIO");
            cbQuantidade = CriarCombo("Qtd", 425, y, 70, new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" });
            cbQuantidade.SelectedIndex = 0;
            y += 62;

            txtCid = CriarTexto("CID-10 *", Margem, y, 100, "Ex: G30");
            txtDiagnostico = CriarTexto("Diagnóstico (busque por código ou nome)", 150, y, 345, "Digite para buscar...");
            lblAvisoCid = new Label {
                AutoSize = true,
                Location = new Point(Margem, y + 52),
                Font = new Font("Segoe UI", 8F),
                Visible = false,
                Tag = "aviso"
            };
            Controls.Add(lblAvisoCid);
            ConfigurarBuscaCid(txtCid);
            ConfigurarBuscaCid(txtDiagnostico);
            y += 72;

            txtObs = CriarTexto("Observações / justificativa", Margem, y, LarguraUtil, "Justificativa clínica para o procedimento...", altura: 90);
            y += 20 + 90 + 12;

            lblStatus = new Label {
                AutoSize = false,
                Location = new Point(Margem, y),
                Size = new Size(LarguraUtil, 20),
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(lblStatus);
            y += 24;

            btnGerar = new Button {
                Text = "GERAR E ABRIR PDF",
                Location = new Point(Margem, y),
                Size = new Size(310, 46),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGerar.FlatAppearance.BorderSize = 0;
            btnGerar.Click += BtnGerar_Click;
            dicas.SetToolTip(btnGerar, "Ctrl+Enter  ·  Enter passa para o próximo campo");
            Controls.Add(btnGerar);

            btnLimpar = new Button {
                Text = "LIMPAR",
                Location = new Point(355, y),
                Size = new Size(140, 46),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLimpar.FlatAppearance.BorderSize = 1;
            btnLimpar.Click += BtnLimpar_Click;
            Controls.Add(btnLimpar);
            y += 46 + 12;

            lnkPasta = new LinkLabel {
                Text = "Abrir pasta dos laudos",
                AutoSize = true,
                Location = new Point(Margem - 2, y),
                Font = new Font("Segoe UI", 9F),
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            lnkPasta.LinkClicked += (s, e) => AbrirPastaLaudos();
            Controls.Add(lnkPasta);

            lnkMesmoPaciente = new LinkLabel {
                Text = "Novo laudo, mesmo paciente",
                AutoSize = true,
                Location = new Point(lnkPasta.Left + TextRenderer.MeasureText(lnkPasta.Text, lnkPasta.Font).Width + 14, y),
                Font = new Font("Segoe UI", 9F),
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            lnkMesmoPaciente.LinkClicked += (s, e) => NovoLaudoMesmoPaciente();
            dicas.SetToolTip(lnkMesmoPaciente, "Mantém profissional e paciente; limpa procedimento, CID e justificativa (Ctrl+N)");
            Controls.Add(lnkMesmoPaciente);

            lblAtalhos = new Label {
                Text = "Ctrl+Enter: gerar PDF",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F),
                Tag = "mutado"
            };
            Controls.Add(lblAtalhos);
            lblAtalhos.Location = new Point(Margem + LarguraUtil - TextRenderer.MeasureText(lblAtalhos.Text, lblAtalhos.Font).Width, y + 2);
            y += 30;

            ClientSize = new Size(Margem * 2 + LarguraUtil, y);
            lstSugestoes.BringToFront();
        }

        private void CriarSecao(string titulo, ref int y)
        {
            var lbl = new Label { Text = titulo, Location = new Point(Margem, y), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            var linha = new Panel { Location = new Point(Margem, y + 20), Size = new Size(LarguraUtil, 1) };
            labelsSecao.Add(lbl);
            divisores.Add(linha);
            Controls.Add(lbl);
            Controls.Add(linha);
            y += 32;
        }

        // Link pequeno na linha do rótulo; avança x para o próximo link
        private LinkLabel CriarLinkAcao(string texto, ref int x, int y, Action acao)
        {
            var lnk = new LinkLabel {
                Text = texto, AutoSize = true, Font = new Font("Segoe UI", 8.5F),
                LinkBehavior = LinkBehavior.HoverUnderline, TabStop = true
            };
            lnk.Location = new Point(x, y);
            x += TextRenderer.MeasureText(texto, lnk.Font).Width + 10;
            lnk.LinkClicked += (s, e) => acao();
            Controls.Add(lnk);
            return lnk;
        }

        private Label CriarRotulo(string texto, int x, int y)
        {
            var lbl = new Label { Text = texto, Location = new Point(x - 2, y), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            Controls.Add(lbl);
            return lbl;
        }

        private TextBox CriarTexto(string rotulo, int x, int y, int largura, string placeholder, int altura = 30)
        {
            CriarRotulo(rotulo, x, y);
            bool multilinha = altura > 30;
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
                Padding = multilinha ? new Padding(6, 5, 2, 4) : new Padding(8, 6, 8, 2)
            };
            caixa.Controls.Add(tb);
            caixa.Click += (s, e) => tb.Focus();
            RegistrarCaixa(tb, caixa);
            tb.TextChanged += (s, e) => { caixa.Erro = false; MarcarAlterado(); };
            return tb;
        }

        private ComboBox CriarCombo(string rotulo, int x, int y, int largura, string[] itens)
        {
            CriarRotulo(rotulo, x, y);
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
                Size = new Size(largura, 30),
                Padding = new Padding(1, 1, 1, 1)
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
        //  Tema
        // =====================================================================

        internal sealed class Tema
        {
            public Color Fundo, Cabecalho, TituloApp, Campo, Borda, Texto, Mutado, Destaque, Erro, Ok, Aviso, Divisor, BotaoSecundario;
            public static readonly Tema Escuro = new Tema {
                Fundo = Color.FromArgb(30, 30, 30), Cabecalho = Color.FromArgb(20, 20, 20), TituloApp = Color.FromArgb(0, 150, 250),
                Campo = Color.FromArgb(45, 45, 48), Borda = Color.FromArgb(78, 78, 84), Texto = Color.FromArgb(240, 240, 240),
                Mutado = Color.FromArgb(150, 150, 155), Destaque = Color.FromArgb(0, 150, 250), Erro = Color.FromArgb(240, 95, 85),
                Ok = Color.FromArgb(90, 200, 120), Aviso = Color.FromArgb(235, 175, 60), Divisor = Color.FromArgb(70, 70, 70),
                BotaoSecundario = Color.FromArgb(45, 45, 48)
            };
            public static readonly Tema Claro = new Tema {
                Fundo = Color.FromArgb(245, 247, 250), Cabecalho = Color.FromArgb(0, 122, 204), TituloApp = Color.White,
                Campo = Color.White, Borda = Color.FromArgb(190, 196, 205), Texto = Color.FromArgb(40, 40, 40),
                Mutado = Color.FromArgb(105, 110, 118), Destaque = Color.FromArgb(0, 122, 204), Erro = Color.FromArgb(200, 45, 35),
                Ok = Color.FromArgb(25, 135, 65), Aviso = Color.FromArgb(170, 100, 0), Divisor = Color.FromArgb(205, 210, 216),
                BotaoSecundario = Color.White
            };
        }

        private void AplicarTema()
        {
            tema = isDarkMode ? Tema.Escuro : Tema.Claro;

            BackColor = tema.Fundo;
            headerPanel.BackColor = tema.Cabecalho;
            lblTituloApp.ForeColor = tema.TituloApp;
            btnAlternarTema.BackColor = tema.Cabecalho;
            btnAlternarTema.ForeColor = Color.White;
            btnAlternarTema.FlatAppearance.MouseOverBackColor = ControlPaint.Light(tema.Cabecalho, 0.15f);
            btnAlternarTema.FlatAppearance.MouseDownBackColor = tema.Cabecalho;
            btnAlternarTema.Text = isDarkMode ? "☀" : "☾";

            foreach (Control c in Controls)
            {
                if (c is CaixaCampo caixa)
                {
                    caixa.CorFundo = tema.Campo; caixa.CorBorda = tema.Borda; caixa.CorFoco = tema.Destaque; caixa.CorErro = tema.Erro;
                    caixa.BackColor = tema.Campo;
                    foreach (Control filho in caixa.Controls)
                    {
                        filho.BackColor = tema.Campo; filho.ForeColor = tema.Texto;
                        if (filho is ComboTema ct) { ct.CorSeta = tema.Mutado; ct.CorSelecao = tema.Destaque; ct.Invalidate(); }
                    }
                    caixa.Invalidate();
                }
                else if (c is Label lbl && !(c is LinkLabel))
                {
                    if (labelsSecao.Contains(lbl)) lbl.ForeColor = tema.Destaque;
                    else if ((lbl.Tag as string) == "mutado") lbl.ForeColor = tema.Mutado;
                    else if ((lbl.Tag as string) == "aviso") lbl.ForeColor = tema.Aviso;
                    else if (lbl != lblStatus) lbl.ForeColor = tema.Texto;
                }
                else if (c is Panel p && divisores.Contains(p)) p.BackColor = tema.Divisor;
            }

            lstSugestoes.BackColor = tema.Campo;
            lstSugestoes.ForeColor = tema.Texto;

            btnGerar.BackColor = tema.Destaque;
            btnGerar.ForeColor = Color.White;
            btnGerar.FlatAppearance.MouseOverBackColor = ControlPaint.Light(tema.Destaque, 0.2f);
            btnLimpar.BackColor = tema.BotaoSecundario;
            btnLimpar.ForeColor = tema.Texto;
            btnLimpar.FlatAppearance.BorderColor = tema.Borda;
            btnLimpar.FlatAppearance.MouseOverBackColor = isDarkMode ? Color.FromArgb(60, 60, 64) : Color.FromArgb(235, 238, 242);

            foreach (var lnk in Controls.OfType<LinkLabel>())
            {
                lnk.LinkColor = tema.Destaque;
                lnk.ActiveLinkColor = tema.Destaque;
                lnk.VisitedLinkColor = tema.Destaque;
                lnk.DisabledLinkColor = tema.Mutado;
            }

            AplicarBarraDeTitulo();

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

        // Preferências simples "chave=valor" (tema e último profissional escolhido)
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
                if (preenchendoCid || !origem.Focused) return;
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
            txtObs.Focus();
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
            txtNome.Focus();
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
            txtProcedimento.Focus();
        }

        private void BtnGerar_Click(object? sender, EventArgs e)
        {
            OcultarSugestoes();
            if (txtCid.Focused || ActiveControl == btnGerar) RealizarBuscaCid();

            // Nome é obrigatório (usado no nome do arquivo)
            if (txtNome.Text.Trim() == "")
            {
                caixas[txtNome].Erro = true;
                MostrarStatus("Informe o nome do paciente.", tema.Erro);
                txtNome.Focus();
                return;
            }

            if (cbProfissional.SelectedItem is not Profissional profissional)
            {
                caixas[cbProfissional].Erro = true;
                MostrarStatus("Cadastre ou escolha o(a) médico(a) solicitante.", tema.Erro);
                cbProfissional.Focus();
                return;
            }

            string dataNasc = txtDataNasc.Text.Trim();
            if (dataNasc != "" && !DataValida(dataNasc, out _))
            {
                caixas[txtDataNasc].Erro = true;
                MostrarStatus("Data de nascimento inválida. Use o formato dd/mm/aaaa.", tema.Erro);
                txtDataNasc.Focus();
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
                if (resp != DialogResult.Yes) { faltando[0].Campo.Focus(); return; }
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

    /// <summary>Painel com borda de 1px que muda de cor no foco e em caso de erro.</summary>
    internal sealed class CaixaCampo : Panel
    {
        private bool focado, erro;
        public Color CorFundo = Color.White, CorBorda = Color.Gray, CorFoco = Color.DodgerBlue, CorErro = Color.Red;

        public CaixaCampo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Focado { get => focado; set { focado = value; Invalidate(); } }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Erro { get => erro; set { if (erro != value) { erro = value; Invalidate(); } } }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(CorFundo);
            Color cor = erro ? CorErro : focado ? CorFoco : CorBorda;
            int espessura = (erro || focado) ? 2 : 1;
            using var pen = new Pen(cor, espessura) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset };
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
