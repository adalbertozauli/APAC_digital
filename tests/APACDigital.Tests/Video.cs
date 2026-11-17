using System.Text;
using Projeto_APAC;
using Projeto_APAC.Models;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

// Vídeo de demonstração (uso manual), sempre com dados fictícios:
//   dotnet run --project tests\APACDigital.Tests -- video        -> grava quadros da janela em saida-testes\video\bruto
//   python scripts\montar-video.py <saida-testes\video>          -> monta as cenas 1920x1080 em saida-testes\video\cenas
//   dotnet run --project tests\APACDigital.Tests -- codificar <pasta das cenas> <arquivo.mp4>
static partial class Testes
{
    static string pastaVideo = "";
    static int numeroQuadro;
    static readonly StringBuilder roteiro = new();

    // Captura a janela e registra quanto tempo o quadro fica na tela, em qual passo e com qual legenda
    static void Quadro(Form f, int ms, int passo, string legenda)
    {
        numeroQuadro++;
        string nome = $"q{numeroQuadro:0000}";
        Capturar(f, Path.Combine("video", "bruto", nome));
        roteiro.AppendLine($"{nome}.png|{ms}|{passo}|{legenda}");
    }

    // Digita aos poucos, gravando um quadro a cada pedaço
    static void Digitar(Form f, Control campo, string texto, int letrasPorQuadro, int msPorQuadro, int passo, string legenda)
    {
        Focar(campo);
        for (int i = 0; i < texto.Length; i += letrasPorQuadro)
        {
            Teclar(texto.Substring(i, Math.Min(letrasPorQuadro, texto.Length - i)));
            Quadro(f, msPorQuadro, passo, legenda);
        }
    }

    static void Video()
    {
        pastaVideo = Path.Combine(Saida, "video");
        if (Directory.Exists(pastaVideo)) Directory.Delete(pastaVideo, true);
        Directory.CreateDirectory(Path.Combine(pastaVideo, "bruto"));

        var prof = new Profissional { Nome = "DRA. ANA EXEMPLO", Cns = CnsValido("70012345678901") };
        RepositorioProfissionais.Salvar(new List<Profissional> { prof, new Profissional { Nome = "DR. CARLOS EXEMPLO", Cns = CnsCarlos } });

        var form = new FormPrincipal();
        formAtual = form;
        form.Show(); Pump();
        form.Activate(); Pump();

        var cbProf = Campo<ComboBox>(form, "cbProfissional");
        var nome = Campo<TextBox>(form, "txtNome");
        var data = Campo<TextBox>(form, "txtDataNasc");
        var sexo = Campo<ComboBox>(form, "cbSexo");
        var raca = Campo<ComboBox>(form, "cbRaca");
        var mae = Campo<TextBox>(form, "txtMae");
        var proc = Campo<TextBox>(form, "txtProcedimento");
        var diag = Campo<TextBox>(form, "txtDiagnostico");
        var obs = Campo<TextBox>(form, "txtObs");

        // 0. Abertura
        Focar(Campo<Button>(form, "btnGerar"));
        Quadro(form, 2200, 0, "");

        // 1. Médico solicitante
        Focar(cbProf);
        cbProf.SelectedIndex = cbProf.Items.Cast<Profissional>().ToList().FindIndex(p => p.Nome == prof.Nome);
        Pump();
        Quadro(form, 2300, 1, "Escolha o(a) médico(a) solicitante. O app lembra a última escolha.");

        // 2. Paciente
        const string lp = "Digite os dados do paciente. Tudo sai em maiúsculas, como no laudo.";
        Digitar(form, nome, "maria de souza exemplo", 2, 90, 2, lp);
        Quadro(form, 500, 2, lp);
        Digitar(form, data, "12081975", 1, 110, 2, "A data é formatada enquanto você digita.");
        Quadro(form, 700, 2, "A data é formatada enquanto você digita.");
        Focar(sexo); sexo.SelectedItem = "F"; Pump();
        Quadro(form, 600, 2, lp);
        Focar(raca); raca.SelectedItem = "PARDA"; Pump();
        Quadro(form, 600, 2, lp);
        Digitar(form, mae, "joana de souza exemplo", 2, 90, 2, lp);
        Quadro(form, 600, 2, lp);

        // 3. Procedimento e CID
        const string lc = "Busque o CID-10 pelo nome da doença ou pelo código.";
        Digitar(form, proc, "ressonância magnética de coluna lombo-sacra", 3, 80, 3, "Informe o procedimento solicitado e a quantidade.");
        Quadro(form, 600, 3, "Informe o procedimento solicitado e a quantidade.");
        Digitar(form, diag, "lombar", 1, 140, 3, lc);
        Quadro(form, 1800, 3, lc);
        Teclar("{ENTER}");
        Quadro(form, 1500, 3, "Enter escolhe a sugestão: CID e descrição são preenchidos juntos.");

        // 4. Justificativa
        const string lj = "Escreva a justificativa clínica. Textos longos se ajustam ao espaço do laudo.";
        Digitar(form, obs, "paciente com lombalgia há 6 meses, irradiada para membro inferior esquerdo, " +
            "sem melhora com analgésicos e fisioterapia. investigar hérnia de disco.", 5, 70, 4, lj);
        Quadro(form, 900, 4, lj);

        // 5. Gerar
        Focar(Campo<Button>(form, "btnGerar"));
        Quadro(form, 900, 5, "Ctrl+Enter (ou o botão) gera o PDF.");
        Campo<Button>(form, "btnGerar").PerformClick(); Pump();
        Quadro(form, 2200, 5, "Pronto: o laudo é salvo na pasta escolhida, com nome e horário.");

        Definir(form, "alteradoDesdeUltimoLaudo", false);
        form.Dispose();

        // 6. O PDF gerado
        string pdf = new DirectoryInfo(FormPrincipal.PastaLaudos).GetFiles("*.pdf").OrderByDescending(a => a.LastWriteTime).First().FullName;
        Renderizar(pdf, Path.Combine(pastaVideo, "laudo.png"));
        roteiro.AppendLine($"laudo.png|4500|6|O laudo oficial, preenchido e pronto para imprimir.");

        File.WriteAllText(Path.Combine(pastaVideo, "roteiro.txt"), roteiro.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{numeroQuadro} quadros salvos em {pastaVideo}");
    }

    // Extrai um quadro do vídeo (conferência): quadro <mp4> <segundos> <png>
    static void ExtrairQuadro(string mp4, double segundos, string png)
    {
        var arquivo = StorageFile.GetFileFromPathAsync(Path.GetFullPath(mp4)).AsTask().Result;
        var clipe = MediaClip.CreateFromFileAsync(arquivo).AsTask().Result;
        var composicao = new MediaComposition();
        composicao.Clips.Add(clipe);
        using var miniatura = composicao.GetThumbnailAsync(TimeSpan.FromSeconds(segundos), 960, 540, VideoFramePrecision.NearestFrame).AsTask().Result;
        using var entrada = miniatura.AsStreamForRead();
        using var saida = File.Create(png);
        entrada.CopyTo(saida);
        Console.WriteLine($"Duração {clipe.OriginalDuration.TotalSeconds:0.0} s; quadro de {segundos} s salvo em {png}");
    }

    // Junta as cenas (lista "arquivo|ms" em roteiro.txt) num MP4 com o codificador do Windows
    static void Codificar(string pastaCenas, string saida)
    {
        var composicao = new MediaComposition();
        foreach (string linha in File.ReadAllLines(Path.Combine(pastaCenas, "roteiro.txt")))
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;
            var partes = linha.Split('|');
            var arquivo = StorageFile.GetFileFromPathAsync(Path.GetFullPath(Path.Combine(pastaCenas, partes[0]))).AsTask().Result;
            composicao.Clips.Add(MediaClip.CreateFromImageFileAsync(arquivo, TimeSpan.FromMilliseconds(int.Parse(partes[1]))).AsTask().Result);
        }
        string caminho = Path.GetFullPath(saida);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllBytes(caminho, Array.Empty<byte>());
        var destino = StorageFile.GetFileFromPathAsync(caminho).AsTask().Result;
        var perfil = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
        perfil.Video.FrameRate.Numerator = 30; perfil.Video.FrameRate.Denominator = 1;
        perfil.Video.Bitrate = 6_000_000;
        var resultado = composicao.RenderToFileAsync(destino, MediaTrimmingPreference.Precise, perfil).AsTask().Result;
        Console.WriteLine($"Vídeo: {caminho} ({resultado}) — {composicao.Duration.TotalSeconds:0.0} s");
    }
}
