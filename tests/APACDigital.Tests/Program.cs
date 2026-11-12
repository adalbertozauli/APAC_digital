using System.Reflection;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Projeto_APAC;
using Projeto_APAC.Models;
using Projeto_APAC.Services;
using Windows.Data.Pdf;
using Windows.Storage;
using WinPdf = Windows.Data.Pdf.PdfDocument;

// Testes do APAC Digital.
//   dotnet run --project tests\APACDigital.Tests            -> roda os testes
//   dotnet run --project tests\APACDigital.Tests -- telas   -> salva capturas da tela e PDFs renderizados em ./saida-testes
static class Testes
{
    static readonly string Base = AppContext.BaseDirectory;
    static readonly string Modelo = Path.Combine(Base, "Templates", "Laudo de APAC - Modelo.pdf");
    static readonly string Saida = Path.Combine(Base, "saida-testes");
    static int falhas, total;

    static void Check(bool ok, string nome)
    {
        total++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + nome);
        if (!ok) falhas++;
    }

    [STAThread]
    static int Main(string[] args)
    {
        Directory.CreateDirectory(Saida);

        // Isola os dados do usuário: cadastro e laudos em pasta própria dos testes;
        // preferências reais são salvas antes e restauradas ao final.
        RepositorioProfissionais.Arquivo = Path.Combine(Saida, "profissionais.json");
        if (File.Exists(RepositorioProfissionais.Arquivo)) File.Delete(RepositorioProfissionais.Arquivo);
        FormPrincipal.PastaLaudos = Path.Combine(Saida, "laudos");
        FormPrincipal.AbrirPdfAoGerar = false;
        string prefs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "APAC Digital", "preferencias.txt");
        string? prefsBackup = File.Exists(prefs) ? File.ReadAllText(prefs) : null;
        if (File.Exists(prefs)) File.Delete(prefs);

        try
        {
            if (args.Contains("telas")) { Telas(); return 0; }
            TestarPdf();
            TestarProfissionais();
            TestarUi();
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERRO INESPERADO: " + ex);
            falhas++;
        }
        finally
        {
            if (prefsBackup != null) File.WriteAllText(prefs, prefsBackup);
            else if (File.Exists(prefs)) File.Delete(prefs);
        }

        Console.WriteLine(falhas == 0 ? $"TODOS OK ({total} testes)" : $"{falhas} FALHA(S) de {total} testes");
        return falhas;
    }

    // =====================================================================
    //  PDF
    // =====================================================================

    static DadosApac Dados(string obs, string cid = "I10") => new DadosApac {
        NomePaciente = "JOÃO DA SILVA", DataNascimento = "05/03/1960", Sexo = "M", RacaCor = "PARDA",
        NomeMae = "MARIA DA SILVA", ProcedimentoPrincipal = "TC DE CRÂNIO", Quantidade = "1",
        Cid10Principal = cid, DescricaoDiagnostico = cid == "" ? "" : "HIPERTENSÃO ESSENCIAL (PRIMÁRIA)", Observacoes = obs,
        NomeProfissional = "DR. ADALBERTO ZAULI DOS SANTOS", CnsProfissional = "703200673914795"
    };

    static string Texto(string path)
    {
        using var pdf = new iText.Kernel.Pdf.PdfDocument(new PdfReader(path));
        return PdfTextExtractor.GetTextFromPage(pdf.GetPage(1));
    }

    static void TestarPdf()
    {
        var svc = new GeradorPdfService();
        string curta = "PACIENTE COM CEFALEIA PERSISTENTE HÁ 3 MESES, REFRATÁRIA A ANALGÉSICOS.";
        string media = string.Join(" ", Enumerable.Repeat("PACIENTE COM CEFALEIA PERSISTENTE HÁ 3 MESES, REFRATÁRIA A ANALGÉSICOS COMUNS.", 6));
        string enorme = string.Join(" ", Enumerable.Repeat("PACIENTE COM CEFALEIA PERSISTENTE HÁ 3 MESES, REFRATÁRIA A ANALGÉSICOS COMUNS.", 60));

        var a1 = svc.PreencherLaudo(Dados(curta), Modelo, Path.Combine(Saida, "curta.pdf"));
        Check(a1.Count == 0, "justificativa curta: sem aviso");
        string t1 = Texto(Path.Combine(Saida, "curta.pdf"));
        Check(t1.Contains("JOÃO DA SILVA") && t1.Contains("I10") && t1.Contains("CEFALEIA"), "PDF contém nome, CID e justificativa");

        Check(svc.PreencherLaudo(Dados(media), Modelo, Path.Combine(Saida, "media.pdf")).Count == 0, "justificativa média: sem aviso");
        Check(svc.PreencherLaudo(Dados(enorme), Modelo, Path.Combine(Saida, "enorme.pdf")).Count == 1, "justificativa enorme: avisa que pode ser cortada");

        svc.PreencherLaudo(Dados(curta, ""), Modelo, Path.Combine(Saida, "semcid.pdf"));
        Check(!Texto(Path.Combine(Saida, "semcid.pdf")).Contains("EX:"), "sem CID: PDF não contém 'EX:'");

        // Campos de uma linha: texto longo reduz a letra; texto absurdo gera aviso
        var longo = Dados(curta);
        longo.ProcedimentoPrincipal = "RESSONANCIA MAGNETICA DE COLUNA LOMBO-SACRA COM CONTRASTE";
        var aLongo = svc.PreencherLaudo(longo, Modelo, Path.Combine(Saida, "procedimento_longo.pdf"));
        Check(aLongo.Count == 0, $"procedimento de 57 letras cabe com letra menor ({aLongo.Count} aviso)");
        Check(Texto(Path.Combine(Saida, "procedimento_longo.pdf")).Contains("COM CONTRASTE"), "procedimento longo aparece inteiro no PDF");
        longo.ProcedimentoPrincipal = string.Join(" ", Enumerable.Repeat("PROCEDIMENTO MUITO LONGO", 6));
        var aAbsurdo = svc.PreencherLaudo(longo, Modelo, Path.Combine(Saida, "procedimento_absurdo.pdf"));
        Check(aAbsurdo.Count == 1 && aAbsurdo[0].Contains("Procedimento"), "procedimento que não cabe nem com letra mínima gera aviso");

        // Arquivo de saída bloqueado: deve lançar erro
        string bloqueado = Path.Combine(Saida, "bloqueado.pdf");
        File.WriteAllText(bloqueado, "x");
        using (File.Open(bloqueado, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            bool lancou = false;
            try { svc.PreencherLaudo(Dados(curta), Modelo, bloqueado); } catch { lancou = true; }
            Check(lancou, "arquivo bloqueado: erro é lançado");
        }
    }

    // =====================================================================
    //  Profissionais
    // =====================================================================

    // Gera um CNS provisório válido (começa com 7) para os testes
    static string CnsValido(string prefixo14)
    {
        int soma = 0;
        for (int i = 0; i < 14; i++) soma += (prefixo14[i] - '0') * (15 - i);
        for (int d = 0; d <= 9; d++) if ((soma + d) % 11 == 0) return prefixo14 + d;
        return CnsValido(prefixo14[..13] + ((prefixo14[13] - '0' + 1) % 10));
    }

    static void TestarProfissionais()
    {
        Check(Profissional.CnsValido("703200673914795"), "CNS do Dr. Adalberto é válido");
        Check(!Profissional.CnsValido("703200673914796"), "CNS com último dígito errado é inválido");
        Check(!Profissional.CnsValido("70320067391479"), "CNS com 14 dígitos é inválido");
        Check(!Profissional.CnsValido("303200673914795"), "CNS começando com 3 é inválido");
        string cnsMaria = CnsValido("70012345678901");
        Check(Profissional.CnsValido(cnsMaria), "CNS gerado para teste é válido");

        var lista = RepositorioProfissionais.Carregar();
        Check(lista.Count == 1 && lista[0].Cns == "703200673914795", "primeiro uso traz o Dr. Adalberto cadastrado");
        lista.Add(new Profissional { Nome = "DRA. TESTE", Cns = cnsMaria });
        RepositorioProfissionais.Salvar(lista);
        Check(RepositorioProfissionais.Carregar().Count == 2, "cadastro salvo e recarregado");
        File.Delete(RepositorioProfissionais.Arquivo);

        var dados = Dados("TESTE");
        dados.NomeProfissional = "DRA. MARIA DE SOUZA";
        dados.CnsProfissional = cnsMaria;
        string pdf = Path.Combine(Saida, "outro_medico.pdf");
        new GeradorPdfService().PreencherLaudo(dados, Modelo, pdf);
        string t = Texto(pdf);
        Check(t.Contains("DRA. MARIA DE SOUZA") && !t.Contains("ADALBERTO"), "PDF usa o profissional escolhido");
        Check(t.Replace(" ", "").Replace("\n", "").Contains(cnsMaria), "PDF contém o CNS do profissional escolhido");
    }

    // =====================================================================
    //  Tela
    // =====================================================================

    static T Campo<T>(object obj, string nome) =>
        (T)obj.GetType().GetField(nome, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;

    static void Definir(object obj, string nome, object valor) =>
        obj.GetType().GetField(nome, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(obj, valor);

    static object? Chamar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(obj, null);

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetFocus();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_CHAR = 0x102;

    // Envia teclas ao controle com foco pela fila de mensagens (não depende da janela estar em primeiro plano)
    static void Teclar(string seq)
    {
        foreach (Match m in Regex.Matches(seq, @"\{(\w+)(?: (\d+))?\}|.", RegexOptions.Singleline))
        {
            if (m.Groups[1].Success)
            {
                int n = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
                Keys k = m.Groups[1].Value switch {
                    "BACKSPACE" => Keys.Back, "ENTER" => Keys.Enter, "DOWN" => Keys.Down, "UP" => Keys.Up,
                    "ESC" => Keys.Escape, "DEL" => Keys.Delete, _ => throw new ArgumentException(m.Value)
                };
                for (int i = 0; i < n; i++)
                {
                    IntPtr h = GetFocus();
                    if (k == Keys.Back) { PostMessage(h, WM_CHAR, (IntPtr)8, IntPtr.Zero); Pump(); continue; }
                    PostMessage(h, WM_KEYDOWN, (IntPtr)k, IntPtr.Zero);
                    PostMessage(h, WM_KEYUP, (IntPtr)k, IntPtr.Zero);
                    Pump();
                }
            }
            else { PostMessage(GetFocus(), WM_CHAR, (IntPtr)m.Value[0], IntPtr.Zero); Application.DoEvents(); }
        }
        Pump();
    }

    static void Pump() { for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(15); } }

    // Executa uma ação que abre a janela de profissional (modal) e a preenche enquanto está aberta
    static void ComDialogo(Action abrir, Action<Form> preencher)
    {
        var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (s, e) => {
            var dlg = Application.OpenForms.OfType<Form>().FirstOrDefault(f => f.GetType().Name == "FormProfissional");
            if (dlg == null) return;
            timer.Stop();
            preencher(dlg);
        };
        timer.Start();
        abrir();
        timer.Stop();
    }

    static void TestarUi()
    {
        var form = new FormPrincipal();
        form.Show(); form.Activate(); Pump();

        var cid = Campo<TextBox>(form, "txtCid");
        var diag = Campo<TextBox>(form, "txtDiagnostico");
        var data = Campo<TextBox>(form, "txtDataNasc");
        var nome = Campo<TextBox>(form, "txtNome");
        var lista = Campo<ListBox>(form, "lstSugestoes");
        var status = Campo<Label>(form, "lblStatus");
        var aviso = Campo<Label>(form, "lblAvisoCid");

        var cids = (System.Collections.ICollection)Campo<object>(form, "cids");
        Check(cids.Count > 14000, $"tabela CID-10 completa carregada ({cids.Count} códigos)");
        Check(cid.Text == "" && cid.PlaceholderText != "", "CID começa vazio, com placeholder nativo");
        Check(nome.Focused, "foco inicial no nome");

        Campo<Button>(form, "btnGerar").PerformClick(); Pump();
        Check(status.Text.Contains("nome"), $"gerar sem nome mostra erro: [{status.Text}]");

        nome.Focus(); Pump();
        Teclar("maria souza");
        Check(nome.Text == "MARIA SOUZA", $"nome em maiúsculas ao digitar: [{nome.Text}]");
        Teclar("{ENTER}");
        Check(data.Focused, "Enter vai do nome para a data");

        // Data
        data.Focus(); Pump();
        Teclar("05031960");
        Check(data.Text == "05/03/1960", $"data formatada ao digitar: [{data.Text}]");
        Teclar("{BACKSPACE 5}");
        Check(data.Text == "05/0", $"Backspace apaga através da barra: [{data.Text}]");
        Teclar("{BACKSPACE 4}");
        Check(data.Text == "", $"Backspace apaga tudo: [{data.Text}]");
        Teclar("12/");
        Check(data.Text == "12", $"barra digitada à mão não duplica: [{data.Text}]");

        // CID -> diagnóstico
        cid.Focus(); Pump(); cid.SelectAll(); Teclar("I10");
        nome.Focus(); Pump();
        Check(diag.Text.StartsWith("HIPERTENSÃO"), $"CID I10 preenche diagnóstico: [{diag.Text}]");
        cid.Focus(); Pump(); cid.SelectAll(); Teclar("A007");          // código inexistente (A00 só tem .0, .1 e .9)
        nome.Focus(); Pump();
        Check(diag.Text == "", $"CID inexistente limpa diagnóstico automático: [{diag.Text}]");
        Check(aviso.Visible, "aviso de CID não encontrado aparece");

        cid.Focus(); Pump(); cid.SelectAll(); Teclar("m545");
        nome.Focus(); Pump();
        Check(cid.Text == "M54.5" && diag.Text == "DOR LOMBAR BAIXA", $"CID sem ponto é reconhecido e formatado: [{cid.Text}] [{diag.Text}]");

        // Busca por nome, sem acento, com Enter
        cid.Focus(); Pump(); cid.SelectAll(); Teclar("{DEL}");
        diag.Focus(); Pump(); diag.SelectAll(); Teclar("{DEL}"); Teclar("hipertensao");
        Check(lista.Visible && lista.Items.Count >= 1, $"busca por nome mostra sugestões ({lista.Items.Count})");
        Teclar("{ENTER}");
        Check(cid.Text == "I10" && diag.Text.StartsWith("HIPERTENSÃO") && !lista.Visible, $"Enter escolhe sugestão: [{cid.Text}] [{diag.Text}]");

        // Palavra no meio do nome: a descrição mais direta vem primeiro
        cid.Focus(); Pump(); cid.SelectAll(); Teclar("{DEL}");
        diag.Focus(); Pump(); diag.SelectAll(); Teclar("{DEL}"); Teclar("lombar");
        Check(lista.Items.Count > 0 && lista.Items[0]!.ToString()!.StartsWith("M54.5"), $"'lombar' sugere primeiro DOR LOMBAR BAIXA: [{(lista.Items.Count > 0 ? lista.Items[0] : "")}]");
        Teclar("{ESC}");
        diag.SelectAll(); Teclar("{DEL}");

        // Busca por código parcial
        cid.Focus(); Pump(); cid.SelectAll(); Teclar("M5");
        Check(lista.Visible && lista.Items.Cast<object>().All(i => i.ToString()!.StartsWith("M5")), $"código parcial sugere M5x ({lista.Items.Count})");
        Teclar("{DOWN}{ENTER}");
        Check(cid.Text == "M51", $"seta para baixo + Enter escolhe o 2º: [{cid.Text}]");
        Teclar("{ESC}");
        diag.Focus(); Pump(); diag.SelectAll(); Teclar("{DEL}");

        // Diagnóstico digitado à mão é mantido
        diag.Focus(); Pump(); Teclar("DIAG MANUAL");
        cid.Focus(); Pump(); cid.SelectAll(); Teclar("A008");
        nome.Focus(); Pump();
        Check(diag.Text == "DIAG MANUAL", $"diagnóstico manual mantido: [{diag.Text}]");

        // ---- Profissionais ----
        var cbProf = Campo<ComboBox>(form, "cbProfissional");
        Check(cbProf.SelectedItem?.ToString()!.StartsWith("DR. ADALBERTO") == true, $"profissional padrão selecionado: [{cbProf.Text}]");

        string cnsNovo = CnsValido("70098765432101");
        string? erroCnsInvalido = null;
        ComDialogo(() => Chamar(form, "CadastrarProfissional"), dlg => {
            var tNome = Campo<TextBox>(dlg, "txtNome"); var tCns = Campo<TextBox>(dlg, "txtCns");
            tNome.Text = "dra. ana   paula lima";
            tCns.Text = "703 2006 7391 4790";               // colado com espaços e dígito errado
            Chamar(dlg, "Salvar");
            erroCnsInvalido = Campo<Label>(dlg, "lblErro").Text;
            tCns.Text = "703200673914795";                   // CNS de outro profissional já cadastrado
            Chamar(dlg, "Salvar");
            Check(Campo<Label>(dlg, "lblErro").Text.Contains("já está cadastrado"), "CNS repetido é recusado");
            tCns.Text = cnsNovo;
            Chamar(dlg, "Salvar");
        });
        Check(erroCnsInvalido?.Contains("inválido") == true, $"CNS inválido é recusado: [{erroCnsInvalido}]");
        Check(cbProf.Items.Count == 2 && cbProf.SelectedItem is Profissional pn && pn.Nome == "DRA. ANA PAULA LIMA" && pn.Cns == cnsNovo,
            $"novo profissional cadastrado e selecionado: [{cbProf.Text}]");
        Check(RepositorioProfissionais.Carregar().Count == 2, "novo profissional salvo em disco");
        string prefsTxt = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "APAC Digital", "preferencias.txt"));
        Check(prefsTxt.Contains("profissional=" + cnsNovo), "escolha do profissional é lembrada");

        using (var form2 = new FormPrincipal())
            Check(Campo<ComboBox>(form2, "cbProfissional").SelectedItem is Profissional p2 && p2.Cns == cnsNovo, "ao reabrir, o último profissional vem selecionado");

        // Links do profissional acessíveis pelo Tab, na ordem visual
        var links = new[] { "lnkNovoProf", "lnkEditarProf", "lnkExcluirProf" }.Select(n => Campo<LinkLabel>(form, n)).ToArray();
        Check(links.All(l => l.TabStop) && links[0].TabIndex < links[1].TabIndex && links[1].TabIndex < links[2].TabIndex
              && links[0].Left < links[1].Left && links[1].Left < links[2].Left, "links Cadastrar/Editar/Excluir acessíveis pelo Tab, na ordem visual");

        // ---- Gerar laudo completo, depois novo laudo para o mesmo paciente ----
        nome.Focus(); Pump(); nome.SelectAll(); Teclar("JOSE TESTE");
        data.Focus(); Pump(); data.SelectAll(); Teclar("01021970");
        Campo<ComboBox>(form, "cbSexo").SelectedIndex = 0;
        var mae = Campo<TextBox>(form, "txtMae"); mae.Text = "ANA TESTE";
        var proc = Campo<TextBox>(form, "txtProcedimento"); proc.Text = "TC DE CRANIO";
        var obs = Campo<TextBox>(form, "txtObs"); obs.Text = "CEFALEIA";
        cid.Focus(); Pump(); cid.SelectAll(); Teclar("G43"); nome.Focus(); Pump();
        Check(Campo<bool>(form, "alteradoDesdeUltimoLaudo"), "dados digitados ficam marcados como não gerados");

        int antes = Directory.Exists(FormPrincipal.PastaLaudos) ? Directory.GetFiles(FormPrincipal.PastaLaudos).Length : 0;
        Campo<Button>(form, "btnGerar").PerformClick(); Pump();
        int depois = Directory.GetFiles(FormPrincipal.PastaLaudos).Length;
        Check(depois == antes + 1 && status.Text.StartsWith("✓ Laudo salvo"), $"laudo gerado sem abrir leitor: [{status.Text}]");
        Check(!Campo<bool>(form, "alteradoDesdeUltimoLaudo"), "após gerar, não há dados pendentes (fechar não pergunta)");

        Chamar(form, "NovoLaudoMesmoPaciente"); Pump();
        Check(nome.Text == "JOSE TESTE" && data.Text == "01/02/1970" && mae.Text == "ANA TESTE", "novo laudo mantém os dados do paciente");
        Check(proc.Text == "" && cid.Text == "" && diag.Text == "" && obs.Text == "", "novo laudo limpa procedimento, CID, diagnóstico e justificativa");
        Check(proc.Focused, "novo laudo põe o foco no procedimento");

        Definir(form, "alteradoDesdeUltimoLaudo", false);   // evita o aviso de fechar
        form.Dispose();
    }

    // =====================================================================
    //  Capturas (uso manual: "-- telas")
    // =====================================================================

    static void Telas()
    {
        var form = new FormPrincipal();
        form.Show(); Pump();
        Capturar(form, "tela_1");
        Campo<Button>(form, "btnAlternarTema").PerformClick(); Pump();
        Capturar(form, "tela_2");
        Campo<Button>(form, "btnAlternarTema").PerformClick(); Pump();
        var diag = Campo<TextBox>(form, "txtDiagnostico");
        diag.Focus(); Pump(); Teclar("lombar");
        Capturar(form, "tela_sugestoes");
        Teclar("{ESC}");
        Definir(form, "alteradoDesdeUltimoLaudo", false);
        form.Dispose();

        var dados = Dados("PACIENTE COM CEFALEIA PERSISTENTE.");
        dados.ProcedimentoPrincipal = "RESSONANCIA MAGNETICA DE COLUNA LOMBO-SACRA COM CONTRASTE";
        string pdf = Path.Combine(Saida, "laudo_exemplo.pdf");
        new GeradorPdfService().PreencherLaudo(dados, Modelo, pdf);
        Renderizar(pdf, Path.Combine(Saida, "laudo_exemplo.png"));
        Console.WriteLine("Capturas salvas em " + Saida);
    }

    static void Capturar(Form f, string nome)
    {
        f.TopMost = true; Pump();
        var r = f.Bounds;
        using var bmp = new Bitmap(r.Width, r.Height);
        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        bmp.Save(Path.Combine(Saida, nome + ".png"));
    }

    static void Renderizar(string pdfPath, string pngPath)
    {
        var file = StorageFile.GetFileFromPathAsync(pdfPath).AsTask().Result;
        var doc = WinPdf.LoadFromFileAsync(file).AsTask().Result;
        using var page = doc.GetPage(0);
        var folder = StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(pngPath)!).AsTask().Result;
        var outFile = folder.CreateFileAsync(Path.GetFileName(pngPath), CreationCollisionOption.ReplaceExisting).AsTask().Result;
        using var stream = outFile.OpenAsync(FileAccessMode.ReadWrite).AsTask().Result;
        page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = (uint)(page.Size.Width * 2) }).AsTask().Wait();
    }
}
