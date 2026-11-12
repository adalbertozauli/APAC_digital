using System;
using System.Drawing;
using System.Windows.Forms;
using Projeto_APAC.Models;
using Projeto_APAC.Services;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace Projeto_APAC
{
    public class FormPrincipal : Form
    {
        private TextBox txtNome = null!, txtDataNasc = null!, txtMae = null!, txtCid = null!, txtDiagnostico = null!, txtProcedimento = null!, txtObs = null!;
        private ComboBox cbSexo = null!, cbRaca = null!, cbQuantidade = null!;
        private Button btnGerar = null!, btnLimpar = null!, btnAlternarTema = null!;
        private Panel headerPanel = null!;
        
        private List<Label> labelsTitulo = new List<Label>();
        private List<Panel> paineisDivisores = new List<Panel>();
        private bool isDarkMode = true;

        // Dicionário para armazenar os textos dos placeholders
        private Dictionary<TextBox, string> placeholders = new Dictionary<TextBox, string>();

        // Último diagnóstico preenchido automaticamente pela busca de CID
        private string? diagnosticoAutomatico;
        private bool formatandoData;

        private static readonly string PastaLaudos = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "APAC Digital", "Laudos");

        public FormPrincipal()
        {
            ConfigurarJanela();
            CriarInterface();
            AplicarTema();
            ConfigurarTodosPlaceholders();
            MigrarLaudosAntigos();
        }

        // Versões anteriores salvavam os laudos em "Saida", dentro da pasta do programa,
        // que é apagada ao desinstalar. Move esses arquivos para a pasta de Documentos.
        private void MigrarLaudosAntigos()
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

        private void ConfigurarJanela()
        {
            this.Text = "APAC Digital - ESF São Carlos 2";
            this.Size = new Size(540, 820);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;

            string caminhoIcone = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icone.ico");
            if (File.Exists(caminhoIcone)) this.Icon = new Icon(caminhoIcone);
        }

        private void CriarInterface()
        {
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 60 };
            Label lblTitulo = new Label { 
                Text = "Emissão de Laudo APAC", 
                Font = new Font("Segoe UI", 14, FontStyle.Bold), 
                AutoSize = true, 
                Location = new Point(20, 15) 
            };
            
            btnAlternarTema = new Button { 
                Text = "🌓", 
                Size = new Size(45, 30), 
                Location = new Point(465, 15), 
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnAlternarTema.FlatAppearance.BorderSize = 0;
            btnAlternarTema.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btnAlternarTema.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnAlternarTema.Click += (s, e) => { isDarkMode = !isDarkMode; AplicarTema(); };
            
            headerPanel.Controls.Add(lblTitulo);
            headerPanel.Controls.Add(btnAlternarTema);
            this.Controls.Add(headerPanel);

            int y = 85;
            CriarSecao("DADOS DO PACIENTE", ref y);
            
            AdicionarLabel("Nome Completo:", 30, y);
            txtNome = AdicionarTextBox(30, y + 22, 465); y += 70;

            AdicionarLabel("Nascimento:", 30, y);
            txtDataNasc = AdicionarTextBox(30, y + 22, 130);
            txtDataNasc.MaxLength = 10;
            txtDataNasc.TextChanged += (s, e) => FormatarData(txtDataNasc);

            AdicionarLabel("Sexo:", 185, y);
            cbSexo = AdicionarCombo(185, y + 22, 80, new[] { "M", "F" });

            AdicionarLabel("Raça/Cor:", 290, y);
            cbRaca = AdicionarCombo(290, y + 22, 205, new[] { "BRANCA", "PRETA", "PARDA", "AMARELA", "INDÍGENA" }); y += 70;

            AdicionarLabel("Nome da Mãe:", 30, y);
            txtMae = AdicionarTextBox(30, y + 22, 465); y += 90;

            CriarSecao("INFORMAÇÕES CLÍNICAS", ref y);
            
            AdicionarLabel("Procedimento Principal:", 30, y);
            txtProcedimento = AdicionarTextBox(30, y + 22, 380);
            
            AdicionarLabel("Qtd:", 425, y);
            cbQuantidade = AdicionarCombo(425, y + 22, 70, new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" });
            cbQuantidade.SelectedIndex = 0; y += 70;

            AdicionarLabel("CID 10:", 30, y);
            txtCid = AdicionarTextBox(30, y + 22, 100);
            txtCid.CharacterCasing = CharacterCasing.Upper;
            txtCid.Leave += (s, e) => RealizarBuscaCid();

            AdicionarLabel("Diagnóstico:", 150, y);
            txtDiagnostico = AdicionarTextBox(150, y + 22, 345); y += 70;

            AdicionarLabel("Observações / Justificativa:", 30, y);
            txtObs = new TextBox { 
                Location = new Point(30, y + 22), 
                Size = new Size(465, 80), 
                Multiline = true, 
                ScrollBars = ScrollBars.Vertical, 
                BorderStyle = BorderStyle.FixedSingle 
            };
            this.Controls.Add(txtObs); y += 125;

            btnGerar = new Button { 
                Text = "GERAR E ABRIR PDF", 
                Location = new Point(30, y), 
                Size = new Size(310, 50), 
                FlatStyle = FlatStyle.Flat, 
                Font = new Font("Segoe UI", 11, FontStyle.Bold), 
                Cursor = Cursors.Hand 
            };
            btnGerar.Click += BtnGerar_Click;
            this.Controls.Add(btnGerar);

            btnLimpar = new Button { 
                Text = "LIMPAR", 
                Location = new Point(355, y), 
                Size = new Size(140, 50), 
                FlatStyle = FlatStyle.Flat, 
                Font = new Font("Segoe UI", 11, FontStyle.Bold), 
                Cursor = Cursors.Hand 
            };
            btnLimpar.Click += BtnLimpar_Click;
            this.Controls.Add(btnLimpar);
        }

        // --- LÓGICA DE PLACEHOLDERS ---
        private void ConfigurarTodosPlaceholders()
        {
            AdicionarPlaceholder(txtNome, "Ex: JOÃO DA SILVA");
            AdicionarPlaceholder(txtDataNasc, "00/00/0000");
            AdicionarPlaceholder(txtMae, "Ex: MARIA DA SILVA");
            AdicionarPlaceholder(txtProcedimento, "Ex: TC DE CRÂNIO");
            AdicionarPlaceholder(txtCid, "Ex: G30");
            AdicionarPlaceholder(txtDiagnostico, "O diagnóstico aparecerá aqui...");
            AdicionarPlaceholder(txtObs, "Justificativa clínica para o procedimento...");
        }

        private void AdicionarPlaceholder(TextBox txt, string placeholder)
        {
            placeholders[txt] = placeholder;
            txt.Text = placeholder;
            txt.ForeColor = Color.Gray;

            txt.Enter += (s, e) => {
                if (EhPlaceholder(txt)) {
                    txt.Text = "";
                    txt.ForeColor = isDarkMode ? Color.FromArgb(240, 240, 240) : Color.FromArgb(40, 40, 40);
                }
            };

            txt.Leave += (s, e) => {
                if (string.IsNullOrWhiteSpace(txt.Text)) {
                    txt.Text = placeholders[txt];
                    txt.ForeColor = Color.Gray;
                }
            };
        }

        // O campo CID converte o texto para maiúsculas (inclusive o placeholder),
        // por isso a comparação ignora maiúsculas/minúsculas.
        private bool EhPlaceholder(TextBox t) =>
            placeholders.TryGetValue(t, out string? p) && string.Equals(t.Text, p, StringComparison.OrdinalIgnoreCase);

        private string ValorReal(TextBox t) => EhPlaceholder(t) ? "" : t.Text.Trim();

        private void AplicarTema()
        {
            Color bg = isDarkMode ? Color.FromArgb(30, 30, 30) : Color.FromArgb(245, 247, 250);
            Color fg = isDarkMode ? Color.FromArgb(240, 240, 240) : Color.FromArgb(40, 40, 40);
            Color inputBg = isDarkMode ? Color.FromArgb(60, 60, 65) : Color.White;
            Color accent = Color.FromArgb(0, 150, 250);
            Color headerBg = isDarkMode ? Color.FromArgb(20, 20, 20) : Color.FromArgb(0, 122, 204);

            this.BackColor = bg;
            headerPanel.BackColor = headerBg;
            btnAlternarTema.BackColor = headerBg;
            btnAlternarTema.ForeColor = Color.White;

            foreach (Control c in this.Controls)
            {
                if (c is TextBox t) { 
                    t.BackColor = inputBg; 
                    // Se o campo tiver placeholder, mantém cinza. Se tiver texto real, usa cor do tema.
                    if (EhPlaceholder(t))
                        t.ForeColor = Color.Gray;
                    else
                        t.ForeColor = fg; 
                }
                else if (c is ComboBox cb) { cb.BackColor = inputBg; cb.ForeColor = fg; }
                else if (c is Label lbl)
                {
                    if (labelsTitulo.Contains(lbl)) lbl.ForeColor = accent;
                    else lbl.ForeColor = fg;
                }
                else if (c is Panel p && paineisDivisores.Contains(p))
                {
                    p.BackColor = isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(200, 200, 200);
                }
            }
            
            headerPanel.Controls[0].ForeColor = isDarkMode ? accent : Color.White; 
            btnGerar.BackColor = accent; btnGerar.ForeColor = Color.White;
            btnLimpar.BackColor = isDarkMode ? Color.FromArgb(70, 70, 75) : Color.FromArgb(231, 76, 60);
            btnLimpar.ForeColor = Color.White;
        }

        private void CriarSecao(string titulo, ref int y)
        {
            Label lbl = new Label { Text = titulo, Location = new Point(30, y), AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
            Panel line = new Panel { Location = new Point(30, y + 18), Size = new Size(465, 1) };
            labelsTitulo.Add(lbl);
            paineisDivisores.Add(line);
            this.Controls.Add(lbl);
            this.Controls.Add(line);
            y += 35;
        }

        private void AdicionarLabel(string t, int x, int y)
        {
            this.Controls.Add(new Label { 
                Text = t, 
                Location = new Point(x, y - 4), 
                AutoSize = true, 
                Font = new Font("Segoe UI", 8.5f) 
            });
        }

        private TextBox AdicionarTextBox(int x, int y, int l)
        {
            TextBox tb = new TextBox { Location = new Point(x, y + 2), Width = l, BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(tb); return tb;
        }

        private ComboBox AdicionarCombo(int x, int y, int l, string[] itens)
        {
            ComboBox cb = new ComboBox { Location = new Point(x, y + 2), Width = l, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            cb.Items.AddRange(itens); this.Controls.Add(cb); return cb;
        }

        private void RealizarBuscaCid()
        {
            string cid = ValorReal(txtCid).ToUpper();
            string? descricao = string.IsNullOrEmpty(cid) ? null : BuscarDescricaoCid(cid);

            if (descricao != null) {
                txtDiagnostico.Text = descricao;
                txtDiagnostico.ForeColor = isDarkMode ? Color.FromArgb(240, 240, 240) : Color.FromArgb(40, 40, 40);
                diagnosticoAutomatico = descricao;
            }
            else if (diagnosticoAutomatico != null && txtDiagnostico.Text == diagnosticoAutomatico) {
                // CID trocado/apagado: remove o diagnóstico que veio do CID anterior.
                // Um diagnóstico digitado à mão é mantido.
                txtDiagnostico.Text = placeholders[txtDiagnostico];
                txtDiagnostico.ForeColor = Color.Gray;
                diagnosticoAutomatico = null;
            }
        }

        private static string? BuscarDescricaoCid(string cid)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cids.txt");
            try {
                if (!File.Exists(path)) return null;
                var match = File.ReadLines(path)
                    .Select(l => l.Split(',', 2))
                    .FirstOrDefault(p => p.Length == 2 && p[0].Trim().ToUpper() == cid);
                return match?[1].Trim().ToUpper();
            } catch (IOException) {
                return null;
            }
        }

        private void BtnLimpar_Click(object? sender, EventArgs e)
        {
            foreach (Control c in this.Controls) {
                if (c is TextBox t && placeholders.ContainsKey(t)) {
                    t.Text = placeholders[t];
                    t.ForeColor = Color.Gray;
                }
            }
            diagnosticoAutomatico = null;
            cbSexo.SelectedIndex = -1; cbRaca.SelectedIndex = -1; cbQuantidade.SelectedIndex = 0;
            txtNome.Focus();
        }

        // Mantém o formato dd/MM/aaaa a partir dos dígitos digitados.
        // As barras são recalculadas a cada mudança, então o Backspace funciona normalmente.
        private void FormatarData(TextBox tb)
        {
            if (formatandoData || EhPlaceholder(tb)) return;

            int digitosAntesDoCursor = tb.Text.Substring(0, Math.Min(tb.SelectionStart, tb.Text.Length)).Count(char.IsDigit);
            string digitos = new string(tb.Text.Where(char.IsDigit).Take(8).ToArray());
            string formatado = digitos.Length switch {
                > 4 => $"{digitos[..2]}/{digitos[2..4]}/{digitos[4..]}",
                > 2 => $"{digitos[..2]}/{digitos[2..]}",
                _ => digitos
            };
            if (formatado == tb.Text) return;

            int cursor = 0, vistos = 0;
            while (cursor < formatado.Length && vistos < digitosAntesDoCursor) {
                if (char.IsDigit(formatado[cursor])) vistos++;
                cursor++;
            }

            formatandoData = true;
            tb.Text = formatado;
            tb.SelectionStart = cursor;
            formatandoData = false;
        }

        private void BtnGerar_Click(object? sender, EventArgs e)
        {
            // Verifica se o nome foi preenchido de verdade
            if (ValorReal(txtNome) == "") {
                MessageBox.Show("Nome obrigatório!");
                return;
            }

            string dataNasc = ValorReal(txtDataNasc);
            if (dataNasc != "" && (!DateTime.TryParseExact(dataNasc, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dtNasc)
                                   || dtNasc > DateTime.Today || dtNasc.Year < 1900)) {
                MessageBox.Show("Data de nascimento inválida. Use o formato dd/mm/aaaa.");
                txtDataNasc.Focus();
                return;
            }

            // Garante que o diagnóstico corresponde ao CID atual (caso o foco ainda esteja no campo CID)
            RealizarBuscaCid();

            var pac = new DadosApac {
                NomePaciente = ValorReal(txtNome).ToUpper(),
                DataNascimento = dataNasc,
                Sexo = cbSexo.Text,
                NomeMae = ValorReal(txtMae).ToUpper(),
                ProcedimentoPrincipal = ValorReal(txtProcedimento).ToUpper(),
                DescricaoDiagnostico = ValorReal(txtDiagnostico).ToUpper(),
                Cid10Principal = ValorReal(txtCid).ToUpper(),
                Observacoes = ValorReal(txtObs).ToUpper(),
                Quantidade = cbQuantidade.Text,
                RacaCor = cbRaca.Text
            };

            string pasta = AppDomain.CurrentDomain.BaseDirectory;
            string nomeArquivo = CriarNomeArquivoSeguro(pac.NomePaciente);
            // Data e hora no nome evitam sobrescrever laudos anteriores (ou um PDF aberto no leitor)
            string outPath = Path.Combine(PastaLaudos, $"Laudo_{nomeArquivo}_{DateTime.Now:yyyy-MM-dd_HHmmss}.pdf");
            try {
                var avisos = new GeradorPdfService().PreencherLaudo(pac, Path.Combine(pasta, "Templates", "Laudo de APAC - Modelo.pdf"), outPath);
                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
                if (avisos.Count > 0)
                    MessageBox.Show(string.Join("\n\n", avisos), "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } catch (Exception ex) { MessageBox.Show("Erro: " + ex.Message); }
        }

        private string CriarNomeArquivoSeguro(string? nomePaciente)
        {
            string nome = string.IsNullOrWhiteSpace(nomePaciente) ? "Paciente" : nomePaciente.Replace(" ", "_");
            foreach (char caractereInvalido in Path.GetInvalidFileNameChars())
            {
                nome = nome.Replace(caractereInvalido, '_');
            }

            return nome;
        }

        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.Run(new FormPrincipal()); }
    }
}
