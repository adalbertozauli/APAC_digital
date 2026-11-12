using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Projeto_APAC.Models
{
    /// <summary>Médico(a) solicitante que aparece nos campos 41 e 44 do laudo.</summary>
    public class Profissional
    {
        public string Nome { get; set; } = "";
        public string Cns { get; set; } = "";

        public override string ToString() => $"{Nome}   ·   CNS {FormatarCns(Cns)}";

        public static string FormatarCns(string cns) =>
            cns.Length == 15 ? $"{cns[..3]} {cns[3..7]} {cns[7..11]} {cns[11..]}" : cns;

        /// <summary>
        /// Valida o Cartão Nacional de Saúde: 15 dígitos, começando com 1, 2, 7, 8 ou 9,
        /// e soma ponderada (dígito × (15 − posição)) divisível por 11.
        /// </summary>
        public static bool CnsValido(string? cns)
        {
            if (cns == null || cns.Length != 15 || !cns.All(char.IsDigit)) return false;
            if ("12789".IndexOf(cns[0]) < 0) return false;
            int soma = 0;
            for (int i = 0; i < 15; i++) soma += (cns[i] - '0') * (15 - i);
            return soma % 11 == 0;
        }
    }

    /// <summary>Lista de profissionais salva em %APPDATA%\APAC Digital\profissionais.json.</summary>
    public static class RepositorioProfissionais
    {
        public static string Arquivo { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "APAC Digital", "profissionais.json");

        // Profissional que já vinha fixo no aplicativo: cadastrado automaticamente no primeiro uso
        private static readonly Profissional Padrao = new Profissional { Nome = "DR. ADALBERTO ZAULI DOS SANTOS", Cns = "703200673914795" };

        private static readonly JsonSerializerOptions Opcoes = new JsonSerializerOptions { WriteIndented = true };

        public static List<Profissional> Carregar()
        {
            try
            {
                if (File.Exists(Arquivo))
                {
                    var lista = JsonSerializer.Deserialize<List<Profissional>>(File.ReadAllText(Arquivo));
                    if (lista != null) return lista.Where(p => !string.IsNullOrWhiteSpace(p.Nome)).ToList();
                }
            }
            catch (Exception) { /* arquivo corrompido: recomeça com o padrão */ }
            return new List<Profissional> { new Profissional { Nome = Padrao.Nome, Cns = Padrao.Cns } };
        }

        public static void Salvar(List<Profissional> lista)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
            string temp = Arquivo + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(lista, Opcoes));
            File.Move(temp, Arquivo, overwrite: true);
        }
    }
}
