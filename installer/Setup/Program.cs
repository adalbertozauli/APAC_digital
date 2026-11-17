using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

namespace APACDigitalInstaller;

internal static class Program
{
    private const string AppName = "APAC Digital";
    // Versão vem do build (-p:Version=...), em vez de ficar fixa no código
    private static readonly string AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    private const string Publisher = "ESF Sao Carlos 2";
    private const string ExeName = "APACDigital.exe";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            string installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "APACDigital");

            string pastaLaudos = EscolherPastaLaudos();

            Directory.CreateDirectory(installDir);
            ExtractPayload(installDir);
            Directory.CreateDirectory(pastaLaudos);
            SalvarPastaLaudos(pastaLaudos);

            string exePath = Path.Combine(installDir, ExeName);
            string uninstallScript = CreateUninstallScript(installDir);

            CreateShortcut(
                Path.Combine(GetStartMenuDirectory(), $"{AppName}.lnk"),
                exePath,
                installDir);

            CreateShortcut(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{AppName}.lnk"),
                exePath,
                installDir);

            RegisterUninstallEntry(installDir, exePath, uninstallScript);

            MessageBox.Show(
                $"{AppName} foi instalado com sucesso.\n\nOs laudos serão salvos em:\n{pastaLaudos}",
                AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Nao foi possivel instalar o {AppName}.\n\n{ex.Message}",
                AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // Mesmo arquivo de preferências lido pelo aplicativo (chave=valor)
    private static readonly string ArquivoPreferencias = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName, "preferencias.txt");

    private const string ChavePastaLaudos = "pastaLaudos=";

    // Pergunta onde salvar os laudos; se o usuário cancelar, mantém a pasta atual (ou a padrão)
    private static string EscolherPastaLaudos()
    {
        string atual = LerPastaLaudosSalva() ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName, "Laudos");

        using var dialogo = new FolderBrowserDialog
        {
            Description = "Escolha a pasta onde os laudos em PDF serão salvos",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(atual) ? atual : Path.GetDirectoryName(atual) ?? atual,
        };

        if (dialogo.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialogo.SelectedPath))
        {
            return dialogo.SelectedPath;
        }

        return atual;
    }

    private static string? LerPastaLaudosSalva()
    {
        try
        {
            if (!File.Exists(ArquivoPreferencias)) return null;
            string? linha = File.ReadLines(ArquivoPreferencias).FirstOrDefault(l => l.StartsWith(ChavePastaLaudos));
            string? valor = linha?.Substring(ChavePastaLaudos.Length).Trim();
            return string.IsNullOrEmpty(valor) ? null : valor;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Grava a pasta escolhida sem apagar as demais preferências (tema, profissional)
    private static void SalvarPastaLaudos(string pasta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ArquivoPreferencias)!);
        var linhas = File.Exists(ArquivoPreferencias)
            ? File.ReadAllLines(ArquivoPreferencias).Where(l => !l.StartsWith(ChavePastaLaudos)).ToList()
            : new List<string>();
        linhas.Add(ChavePastaLaudos + pasta);
        File.WriteAllLines(ArquivoPreferencias, linhas);
    }

    private static void ExtractPayload(string installDir)
    {
        string payloadPath = Path.Combine(Path.GetTempPath(), $"APACDigital-{Guid.NewGuid():N}.zip");

        try
        {
            using Stream payload = GetPayloadStream();
            using FileStream file = File.Create(payloadPath);
            payload.CopyTo(file);
            file.Close();

            ZipFile.ExtractToDirectory(payloadPath, installDir, overwriteFiles: true);
        }
        finally
        {
            if (File.Exists(payloadPath))
            {
                File.Delete(payloadPath);
            }
        }
    }

    private static Stream GetPayloadStream()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("APACDigital.zip", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException("Pacote do aplicativo nao foi encontrado dentro do instalador.");
        }

        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Nao foi possivel abrir o pacote do aplicativo.");
    }

    private static string CreateUninstallScript(string installDir)
    {
        string uninstallScript = Path.Combine(installDir, "Uninstall-APACDigital.ps1");
        string content = """
            $ErrorActionPreference = "Stop"
            $installDir = Join-Path $env:LOCALAPPDATA "APACDigital"
            $startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\APAC Digital"
            $desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "APAC Digital.lnk"
            Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $startMenuDir -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\APACDigital" -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction SilentlyContinue
            """;

        File.WriteAllText(uninstallScript, content);
        return uninstallScript;
    }

    private static string GetStartMenuDirectory()
    {
        string startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            AppName);

        Directory.CreateDirectory(startMenuDir);
        return startMenuDir;
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            throw new InvalidOperationException("Nao foi possivel acessar o criador de atalhos do Windows.");
        }

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = targetPath;
        shortcut.Save();
    }

    private static void RegisterUninstallEntry(string installDir, string exePath, string uninstallScript)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\APACDigital");

        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", AppVersion);
        key.SetValue("Publisher", Publisher);
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", exePath);
        key.SetValue("UninstallString", $"powershell.exe -ExecutionPolicy Bypass -File \"{uninstallScript}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }
}
