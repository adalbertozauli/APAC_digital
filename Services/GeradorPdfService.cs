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
        public void PreencherLaudo(DadosApac dados, string caminhoModelo, string caminhoSaida)
        {
            if (!Directory.Exists(Path.GetDirectoryName(caminhoSaida))) 
                Directory.CreateDirectory(Path.GetDirectoryName(caminhoSaida)!);

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
                SetCampo(campos, "txtMedico", "DR. ADALBERTO ZAULI DOS SANTOS", 9);
                SetCampo(campos, "txtCnsX", "X", 10);

                // --- CAMPO 44: CNS PROFISSIONAL (c1 a c15) ---
                string cnsVal = "703200673914795";
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
                SetCampo(campos, "txtObs", dados.Observacoes, 8);

                // Achata o formulário para tornar os dados permanentes
                form.FlattenFields();
                pdf.Close();
            }
        }

        private void SetCampo(IDictionary<string, PdfFormField> campos, string nome, string? valor, float tamanhoFonte)
        {
            if (campos.ContainsKey(nome) && !string.IsNullOrWhiteSpace(valor))
            {
                string valorFormatado = valor.Replace("\r\n", "\n").Replace("\r", "\n");
                if (nome == "txtObs") campos[nome].SetFieldFlags(PdfTextFormField.FF_MULTILINE);
                campos[nome].SetFontSize(tamanhoFonte);
                campos[nome].SetValue(valorFormatado);
            }
        }
    }
}