using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Projeto_APAC.Models;

namespace Projeto_APAC
{
    /// <summary>Janela para cadastrar ou editar um(a) médico(a) solicitante.</summary>
    internal sealed class FormProfissional : Form
    {
        private readonly TextBox txtNome, txtCns;
        private readonly CaixaCampo caixaNome, caixaCns;
        private readonly Label lblCnsStatus, lblErro;
        private readonly FormPrincipal.Tema tema;
        private readonly IReadOnlyCollection<Profissional> outros;

        public Profissional? Resultado { get; private set; }

        public FormProfissional(FormPrincipal.Tema tema, IReadOnlyCollection<Profissional> outros, Profissional? existente = null)
        {
            this.tema = tema;
            this.outros = outros;

            Text = existente == null ? "Cadastrar profissional" : "Editar profissional";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = tema.Fundo;

            const int m = 24, largura = 372;
            int y = 18;

            Controls.Add(new Label {
                Text = existente == null ? "Novo(a) médico(a) solicitante" : "Editar médico(a) solicitante",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold), AutoSize = true, Location = new Point(m - 2, y), ForeColor = tema.Destaque
            });
            y += 36;

            Controls.Add(Rotulo("Nome completo (como deve sair no laudo) *", m, y));
            txtNome = NovoTexto("Ex: DRA. MARIA DE SOUZA", out caixaNome, m, y + 20, largura);
            txtNome.CharacterCasing = CharacterCasing.Upper;
            txtNome.MaxLength = 60;
            y += 62;

            Controls.Add(Rotulo("Número do CNS (15 dígitos) *", m, y));
            txtCns = NovoTexto("000 0000 0000 0000", out caixaCns, m, y + 20, 190);
            txtCns.MaxLength = 15;
            txtCns.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
            txtCns.TextChanged += (s, e) => {
                // Remove o que não for dígito (ex.: CNS colado com espaços)
                string digitos = new string(txtCns.Text.Where(char.IsDigit).Take(15).ToArray());
                if (digitos != txtCns.Text) { txtCns.Text = digitos; txtCns.SelectionStart = digitos.Length; return; }
                AtualizarStatusCns();
            };
            lblCnsStatus = new Label { AutoSize = true, Location = new Point(m + 202, y + 27), Font = new Font("Segoe UI", 9F) };
            Controls.Add(lblCnsStatus);
            y += 62;

            lblErro = new Label { AutoSize = false, Size = new Size(largura, 20), Location = new Point(m, y), ForeColor = tema.Erro };
            Controls.Add(lblErro);
            y += 26;

            var btnSalvar = new Button {
                Text = "SALVAR", Location = new Point(m, y), Size = new Size(230, 40), FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold), BackColor = tema.Destaque, ForeColor = Color.White, Cursor = Cursors.Hand
            };
            btnSalvar.FlatAppearance.BorderSize = 0;
            btnSalvar.Click += (s, e) => Salvar();
            Controls.Add(btnSalvar);

            var btnCancelar = new Button {
                Text = "CANCELAR", Location = new Point(m + 242, y), Size = new Size(130, 40), FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold), BackColor = tema.BotaoSecundario, ForeColor = tema.Texto,
                DialogResult = DialogResult.Cancel, Cursor = Cursors.Hand
            };
            btnCancelar.FlatAppearance.BorderColor = tema.Borda;
            Controls.Add(btnCancelar);
            y += 40 + 20;

            AcceptButton = btnSalvar;
            CancelButton = btnCancelar;
            ClientSize = new Size(largura + m * 2, y);

            if (existente != null) { txtNome.Text = existente.Nome; txtCns.Text = existente.Cns; }
            AtualizarStatusCns();
        }

        private Label Rotulo(string texto, int x, int y) =>
            new Label { Text = texto, AutoSize = true, Location = new Point(x - 2, y), Font = new Font("Segoe UI", 8.5F), ForeColor = tema.Texto };

        private TextBox NovoTexto(string placeholder, out CaixaCampo caixa, int x, int y, int largura)
        {
            var tb = new TextBox {
                BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10F), PlaceholderText = placeholder,
                Dock = DockStyle.Fill, BackColor = tema.Campo, ForeColor = tema.Texto
            };
            var c = new CaixaCampo {
                Location = new Point(x, y), Size = new Size(largura, 30), Padding = new Padding(8, 6, 8, 2),
                CorFundo = tema.Campo, CorBorda = tema.Borda, CorFoco = tema.Destaque, CorErro = tema.Erro, BackColor = tema.Campo
            };
            c.Controls.Add(tb);
            c.Click += (s, e) => tb.Focus();
            tb.Enter += (s, e) => c.Focado = true;
            tb.Leave += (s, e) => c.Focado = false;
            tb.TextChanged += (s, e) => { c.Erro = false; lblErro.Text = ""; };
            Controls.Add(c);
            caixa = c;
            return tb;
        }

        private void AtualizarStatusCns()
        {
            string cns = txtCns.Text;
            if (cns.Length < 15) { lblCnsStatus.Text = cns.Length == 0 ? "" : $"{cns.Length}/15 dígitos"; lblCnsStatus.ForeColor = tema.Mutado; }
            else if (Profissional.CnsValido(cns)) { lblCnsStatus.Text = "✓ CNS válido"; lblCnsStatus.ForeColor = tema.Ok; }
            else { lblCnsStatus.Text = "✗ CNS inválido"; lblCnsStatus.ForeColor = tema.Erro; }
        }

        private void Salvar()
        {
            string nome = string.Join(" ", txtNome.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
            string cns = txtCns.Text;

            if (nome.Length < 5 || !nome.Contains(' '))
            {
                caixaNome.Erro = true; lblErro.Text = "Informe o nome completo."; txtNome.Focus(); return;
            }
            if (!Profissional.CnsValido(cns))
            {
                caixaCns.Erro = true;
                lblErro.Text = cns.Length != 15 ? "O CNS deve ter 15 dígitos." : "CNS inválido. Confira os números.";
                txtCns.Focus(); return;
            }
            var repetido = outros.FirstOrDefault(p => p.Cns == cns);
            if (repetido != null)
            {
                caixaCns.Erro = true; lblErro.Text = "Este CNS já está cadastrado para " + repetido.Nome + "."; txtCns.Focus(); return;
            }

            Resultado = new Profissional { Nome = nome, Cns = cns };
            DialogResult = DialogResult.OK;
        }
    }
}
