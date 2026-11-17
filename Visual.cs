using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Projeto_APAC
{
    /// <summary>Utilitários de desenho do visual "vidro" (cantos arredondados, ícones, fundo do pai).</summary>
    internal static class Visual
    {
        // Ícones da fonte do Windows: Segoe Fluent Icons (Windows 11) ou Segoe MDL2 Assets (Windows 10)
        public const string IconeContato = "", IconePessoas = "", IconeSaude = "", IconeDocumento = "",
            IconeCalendario = "", IconeBusca = "", IconeEtiqueta = "", IconeNota = "", IconeCartao = "",
            IconeDiagnostico = "", IconePasta = "", IconeCopiar = "", IconeBorracha = "",
            IconeLua = "", IconeSol = "", IconeSalvar = "", IconeFechar = "";

        private static readonly string familiaIcones = new InstalledFontCollection().Families
            .Select(f => f.Name).FirstOrDefault(n => n == "Segoe Fluent Icons")
            ?? "Segoe MDL2 Assets";

        public static Font FonteIcone(float tamanho) => new Font(familiaIcones, tamanho, FontStyle.Regular, GraphicsUnit.Point);

        public static GraphicsPath Arredondado(RectangleF r, float raio)
        {
            float d = Math.Min(raio * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Alfa(Color c, int a) => Color.FromArgb(Math.Clamp(a, 0, 255), c.R, c.G, c.B);

        public static Color Misturar(Color a, Color b, float t) => Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

        /// <summary>Sombra suave: várias camadas arredondadas com pouca opacidade.</summary>
        public static void Sombra(Graphics g, RectangleF r, float raio, Color cor, int camadas, float deslocY)
        {
            for (int i = camadas; i >= 1; i--)
            {
                var ri = new RectangleF(r.X - i, r.Y - i + deslocY, r.Width + i * 2, r.Height + i * 2);
                using var path = Arredondado(ri, raio + i);
                using var b = new SolidBrush(Alfa(cor, cor.A / camadas));
                g.FillPath(b, path);
            }
        }

        /// <summary>Pinta, no controle, o fundo do formulário que está atrás dele (para cantos arredondados).</summary>
        public static void PintarFundoDoPai(Control c, Graphics g)
        {
            if (c.Parent is IFundoPintavel fundo)
            {
                var estado = g.Save();
                g.TranslateTransform(-c.Left, -c.Top);
                fundo.PintarFundo(g);
                g.Restore(estado);
            }
            else if (c.Parent != null)
            {
                using var b = new SolidBrush(c.Parent.BackColor);
                g.FillRectangle(b, 0, 0, c.Width, c.Height);
            }
        }

        // GDI+ com antisserrilhado: funciona também sobre bitmaps com transparência (o TextRenderer escurece as bordas)
        public static void DesenharIcone(Graphics g, string icone, Font fonte, Color cor, RectangleF area)
        {
            var antes = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var formato = new StringFormat(StringFormat.GenericTypographic) {
                Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
            };
            using var pincel = new SolidBrush(cor);
            g.DrawString(icone, fonte, pincel, area, formato);
            g.TextRenderingHint = antes;
        }
    }

    /// <summary>Formulário que sabe pintar o próprio fundo em qualquer Graphics (usado pelos controles com cantos arredondados).</summary>
    internal interface IFundoPintavel
    {
        void PintarFundo(Graphics g);
    }

    /// <summary>Botão desenhado: primário (degradê), secundário (contorno) ou só ícone (tema).</summary>
    internal sealed class BotaoModerno : Button
    {
        public enum Estilos { Primario, Secundario, Icone }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Estilos Estilo { get; set; } = Estilos.Primario;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Icone { get; set; } = "";
        public Color CorA = Color.DodgerBlue, CorB = Color.RoyalBlue, CorTexto = Color.White, CorFundo = Color.White, CorBorda = Color.SteelBlue;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public float Raio { get; set; } = 13;
        private bool sobre, pressionado;

        public BotaoModerno()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { sobre = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { sobre = false; pressionado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressionado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressionado = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Visual.PintarFundoDoPai(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            if (pressionado) r.Inflate(-0.8f, -0.8f);
            using var path = Visual.Arredondado(r, Raio);

            if (Estilo == Estilos.Primario)
            {
                Color a = sobre ? Visual.Misturar(CorA, Color.White, 0.12f) : CorA;
                Color b = sobre ? Visual.Misturar(CorB, Color.White, 0.10f) : CorB;
                using (var br = new LinearGradientBrush(r, a, b, LinearGradientMode.Vertical)) g.FillPath(br, path);
                using var brilho = new Pen(Color.FromArgb(70, 255, 255, 255), 1f);
                g.DrawLine(brilho, r.X + Raio, r.Y + 1.2f, r.Right - Raio, r.Y + 1.2f);
            }
            else
            {
                Color fundo = sobre ? Visual.Misturar(CorFundo, CorBorda, 0.12f) : CorFundo;
                using (var br = new SolidBrush(fundo)) g.FillPath(br, path);
                using var pen = new Pen(CorBorda, Estilo == Estilos.Secundario ? 1.5f : 1f);
                g.DrawPath(pen, path);
            }

            // Ícone + texto centralizados
            using var fonteIcone = Visual.FonteIcone(Estilo == Estilos.Icone ? 13f : 12.5f);
            var tamIcone = string.IsNullOrEmpty(Icone) ? Size.Empty : TextRenderer.MeasureText(g, Icone, fonteIcone, Size.Empty, TextFormatFlags.NoPadding);
            var tamTexto = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            int espaco = tamIcone.IsEmpty || tamTexto.IsEmpty ? 0 : 8;
            int x = (Width - tamIcone.Width - espaco - tamTexto.Width) / 2;
            if (!tamIcone.IsEmpty)
                Visual.DesenharIcone(g, Icone, fonteIcone, CorTexto, new RectangleF(x, 0, tamIcone.Width, Height));
            if (!tamTexto.IsEmpty)
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x + tamIcone.Width + espaco, 0, tamTexto.Width + 2, Height), CorTexto,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                var rf = RectangleF.Inflate(r, -3, -3);
                using var pf = Visual.Arredondado(rf, Raio - 3);
                using var pen = new Pen(Color.FromArgb(150, CorTexto), 1f) { DashStyle = DashStyle.Dot };
                g.DrawPath(pen, pf);
            }
        }
    }

    /// <summary>Mensagem de status em uma "pílula" com fundo na cor da mensagem.</summary>
    internal sealed class RotuloStatus : Label
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Icone { get; set; } = "";

        public RotuloStatus()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Visual.PintarFundoDoPai(this, g);
            if (string.IsNullOrEmpty(Text)) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using var path = Visual.Arredondado(r, 10);
            using (var b = new SolidBrush(Color.FromArgb(34, ForeColor))) g.FillPath(b, path);
            using (var p = new Pen(Color.FromArgb(70, ForeColor), 1f)) g.DrawPath(p, path);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(10, 0, Width - 18, Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
