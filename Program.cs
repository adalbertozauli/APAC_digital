using System;
using System.Drawing;
using System.Windows.Forms;
using Projeto_APAC.Models;
using Projeto_APAC.Services;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

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

        public FormPrincipal()
        {
            ConfigurarJanela();
            CriarInterface();
            AplicarTema();
            ConfigurarTodosPlaceholders();
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
                if (txt.Text == placeholders[txt]) {
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
                    if (placeholders.ContainsKey(t) && t.Text == placeholders[t])
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
            // Ignora a busca se o texto no campo for apenas o placeholder
            if (txtCid.Text == placeholders[txtCid]) return;

            string cid = txtCid.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(cid)) return;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cids.txt");
            if (File.Exists(path)) {
                var match = File.ReadLines(path)
                    .Select(l => l.Split(',', 2))
                    .FirstOrDefault(p => p.Length == 2 && p[0].Trim().ToUpper() == cid);
                if (match != null) {
                    txtDiagnostico.Text = match[1].Trim().ToUpper();
                    txtDiagnostico.ForeColor = isDarkMode ? Color.FromArgb(240, 240, 240) : Color.FromArgb(40, 40, 40);
                }
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
            cbSexo.SelectedIndex = -1; cbRaca.SelectedIndex = -1; cbQuantidade.SelectedIndex = 0;
            txtNome.Focus();
        }

        private void FormatarData(TextBox tb)
        {
            // Ignora formatação se for placeholder
            if (tb.Text == placeholders[txtDataNasc]) return;

            string d = tb.Text.Replace("/", "");
            if (d.Length >= 2 && tb.Text.Length == 2) tb.Text += "/";
            else if (d.Length >= 4 && tb.Text.Length == 5) tb.Text += "/";
            tb.SelectionStart = tb.Text.Length;
        }

        private void BtnGerar_Click(object? sender, EventArgs e)
        {
            // Verifica se o nome foi preenchido de verdade
            if (string.IsNullOrWhiteSpace(txtNome.Text) || txtNome.Text == placeholders[txtNome]) { 
                MessageBox.Show("Nome obrigatório!"); 
                return; 
            }

            var pac = new DadosApac {
                NomePaciente = txtNome.Text.Trim().ToUpper(),
                // Se o campo contiver o placeholder, enviamos vazio para o PDF
                DataNascimento = (txtDataNasc.Text == placeholders[txtDataNasc]) ? "" : txtDataNasc.Text,
                Sexo = cbSexo.Text,
                NomeMae = (txtMae.Text == placeholders[txtMae]) ? "" : txtMae.Text.Trim().ToUpper(),
                ProcedimentoPrincipal = (txtProcedimento.Text == placeholders[txtProcedimento]) ? "" : txtProcedimento.Text.Trim().ToUpper(),
                DescricaoDiagnostico = (txtDiagnostico.Text == placeholders[txtDiagnostico]) ? "" : txtDiagnostico.Text.Trim().ToUpper(),
                Cid10Principal = (txtCid.Text == placeholders[txtCid]) ? "" : txtCid.Text.Trim().ToUpper(),
                Observacoes = (txtObs.Text == placeholders[txtObs]) ? "" : txtObs.Text.Trim().ToUpper(),
                Quantidade = cbQuantidade.Text,
                RacaCor = cbRaca.Text
            };

            string pasta = AppDomain.CurrentDomain.BaseDirectory;
            string nomeArquivo = CriarNomeArquivoSeguro(pac.NomePaciente);
            string outPath = Path.Combine(pasta, "Saida", $"Laudo_{nomeArquivo}.pdf");
            try {
                new GeradorPdfService().PreencherLaudo(pac, Path.Combine(pasta, "Templates", "Laudo de APAC - Modelo.pdf"), outPath);
                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
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
