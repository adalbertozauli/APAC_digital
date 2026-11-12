using iText.IO.Font.Constants;
using iText.Kernel.Font;
// Alias evita conflito com System.Drawing.Rectangle (using implícito do Windows Forms)
using Rectangle = iText.Kernel.Geom.Rectangle;
using iText.Kernel.Pdf;
using iText.Forms;
using iText.Forms.Fields;
using Projeto_APAC.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace Projeto_APAC.Services
{
    public class GeradorPdfService
    {
        private const float FonteObsMaxima = 8f;
        private const float FonteObsMinima = 5f;

        /// <summary>Preenche o laudo e devolve avisos para mostrar ao usuário (lista vazia se tudo certo).</summary>
        public List<string> PreencherLaudo(DadosApac dados, string caminhoModelo, string caminhoSaida)
        {
            var avisos = new List<string>();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(caminhoSaida)!);

            try
            {
                using (var reader = new PdfReader(caminhoModelo))
                using (var writer = new PdfWriter(caminhoSaida))
                using (var pdf = new PdfDocument(reader, writer))
                {
                    // Criar o formulário e garantir que as aparências sejam geradas
                    PdfAcroForm form = PdfAcroForm.GetAcroForm(pdf, true);
                    form.SetGenerateAppearance(true);

                    IDictionary<string, PdfFormField> campos = form.GetAllFormFields();

                    // --- DADOS FIXOS ---
                    SetCampo(campos, "txtUnidade", "ESF SÃO CARLOS 2", 9);
                    SetCampo(campos, "txtMedico", dados.NomeProfissional, 9);

                    // --- CAMPO 44: CNS PROFISSIONAL (c1 a c15) ---
                    string cnsVal = dados.CnsProfissional ?? "";
                    if (cnsVal != "") SetCampo(campos, "txtCnsX", "X", 10);
                    for (int i = 0; i < 15; i++)
                    {
                        string nomeCns = $"c{i + 1}";
                        if (campos.ContainsKey(nomeCns))
                        {
                            // Extrai cada dígito e preenche a célula correspondente
                            char digito = i < cnsVal.Length ? cnsVal[i] : ' ';
                            campos[nomeCns].SetFontSize(10);
                            campos[nomeCns].SetValue(digito.ToString());
                        }
                    }

                    // --- DATAS (SOLICITAÇÃO) ---
                    DateTime hoje = DateTime.Now;
                    SetCampo(campos, "txtDia", hoje.ToString("dd"), 10);
                    SetCampo(campos, "txtMes", hoje.ToString("MM"), 10);
                    SetCampo(campos, "txtAno", hoje.ToString("yyyy"), 10);

                    // --- DADOS DO PACIENTE ---
                    SetCampo(campos, "txtNome", dados.NomePaciente, 9);
                    SetCampo(campos, "txtRacaCor", dados.RacaCor, 9);
                    SetCampo(campos, "txtNomeMae", dados.NomeMae, 9);

                    if (DateTime.TryParseExact(dados.DataNascimento, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dtNasc))
                    {
                        SetCampo(campos, "txtDiaNasc", dtNasc.ToString("dd"), 10);
                        SetCampo(campos, "txtMesNasc", dtNasc.ToString("MM"), 10);
                        SetCampo(campos, "txtAnoNasc", dtNasc.ToString("yyyy"), 10);
                    }

                    if (dados.Sexo?.ToUpper() == "M") SetCampo(campos, "txtMasc", "X", 10);
                    else if (dados.Sexo?.ToUpper() == "F") SetCampo(campos, "txtFem", "X", 10);

                    // --- DADOS CLÍNICOS ---
                    SetCampo(campos, "txtProcedimento", dados.ProcedimentoPrincipal, 9);
                    SetCampo(campos, "txtQuantidade", dados.Quantidade, 9);
                    SetCampo(campos, "txtCid", dados.Cid10Principal, 9);
                    SetCampo(campos, "txtDiagnostico", dados.DescricaoDiagnostico, 9);

                    // A justificativa pode ser longa: reduz a fonte até caber no campo.
                    float fonteObs = FonteObsMaxima;
                    if (!string.IsNullOrWhiteSpace(dados.Observacoes) && campos.TryGetValue("txtObs", out PdfFormField? campoObs))
                    {
                        Rectangle? area = ObterAreaDoCampo(campoObs);
                        if (area != null)
                        {
                            string texto = NormalizarQuebras(dados.Observacoes);
                            float? cabe = CalcularFonteQueCabe(texto, area, PdfFontFactory.CreateFont(StandardFonts.HELVETICA));
                            if (cabe.HasValue) fonteObs = cabe.Value;
                            else
                            {
                                fonteObs = FonteObsMinima;
                                avisos.Add("A justificativa é longa demais para o espaço do laudo e pode ter sido cortada no PDF. Revise o laudo ou resuma o texto.");
                            }
                        }
                    }
                    SetCampo(campos, "txtObs", dados.Observacoes, fonteObs);

                    // Achata o formulário para tornar os dados permanentes
                    form.FlattenFields();
                }
            }
            catch
            {
                // Não deixa um PDF incompleto/corrompido para trás
                try { if (File.Exists(caminhoSaida)) File.Delete(caminhoSaida); } catch (IOException) { }
                throw;
            }

            return avisos;
        }

        private void SetCampo(IDictionary<string, PdfFormField> campos, string nome, string? valor, float tamanhoFonte)
        {
            if (campos.ContainsKey(nome) && !string.IsNullOrWhiteSpace(valor))
            {
                string valorFormatado = NormalizarQuebras(valor);
                if (nome == "txtObs") campos[nome].SetFieldFlags(PdfTextFormField.FF_MULTILINE);
                campos[nome].SetFontSize(tamanhoFonte);
                campos[nome].SetValue(valorFormatado);
            }
        }

        private static string NormalizarQuebras(string valor) => valor.Replace("\r\n", "\n").Replace("\r", "\n");

        private static Rectangle? ObterAreaDoCampo(PdfFormField campo)
        {
            var widgets = campo.GetWidgets();
            if (widgets == null || widgets.Count == 0) return null;
            return widgets[0].GetRectangle()?.ToRectangle();
        }

        /// <summary>
        /// Maior tamanho de fonte (entre o máximo e o mínimo) em que o texto cabe no campo,
        /// com quebra automática de linha. Retorna null se não couber nem no mínimo.
        /// </summary>
        private static float? CalcularFonteQueCabe(string texto, Rectangle area, PdfFont fonte)
        {
            // Margens internas e entrelinha usadas de forma conservadora
            float largura = area.GetWidth() - 6;
            float altura = area.GetHeight() - 4;

            for (float tamanho = FonteObsMaxima; tamanho >= FonteObsMinima; tamanho -= 0.5f)
            {
                int linhas = ContarLinhas(texto, fonte, tamanho, largura);
                if (linhas * tamanho * 1.2f <= altura) return tamanho;
            }
            return null;
        }

        private static int ContarLinhas(string texto, PdfFont fonte, float tamanho, float largura)
        {
            int total = 0;
            float larguraEspaco = fonte.GetWidth(" ", tamanho);

            foreach (string paragrafo in texto.Split('\n'))
            {
                int linhas = 1;
                float atual = 0;
                foreach (string palavra in paragrafo.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    float w = fonte.GetWidth(palavra, tamanho);
                    if (w > largura)
                    {
                        // Palavra maior que a linha: ocupa várias linhas sozinha
                        if (atual > 0) linhas++;
                        linhas += (int)Math.Ceiling(w / largura) - 1;
                        atual = w % largura;
                        continue;
                    }

                    float necessario = atual == 0 ? w : atual + larguraEspaco + w;
                    if (necessario > largura)
                    {
                        linhas++;
                        atual = w;
                    }
                    else atual = necessario;
                }
                total += linhas;
            }
            return total;
        }
    }
}
