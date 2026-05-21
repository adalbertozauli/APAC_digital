# APAC Digital

Aplicativo Windows para preencher e gerar laudos APAC em PDF para a ESF Sao Carlos 2.

## Requisitos

- Windows x64.
- .NET SDK 10 para compilar a partir do codigo-fonte.

O executavel publicado e `self-contained`, entao o usuario final nao precisa instalar o .NET Runtime.

## Como executar em desenvolvimento

```powershell
dotnet restore .\apac_biblioteca.csproj
dotnet build .\apac_biblioteca.csproj
dotnet run --project .\apac_biblioteca.csproj
```

## Como publicar

```powershell
.\scripts\publish.ps1
```

Os arquivos finais ficam em:

```text
artifacts\publish\APACDigital
```

Essa pasta inclui o executavel, o `cids.txt` e o modelo PDF em `Templates`.

## Como instalar

Opcao simples, sem administrador:

```powershell
.\scripts\install.ps1
```

O instalador local publica o app, copia os arquivos para `%LOCALAPPDATA%\APACDigital` e cria atalhos no menu Iniciar e na area de trabalho.

Para remover:

```powershell
.\scripts\uninstall.ps1
```

## Instalador com assistente

Tambem existe um roteiro para Inno Setup em:

```text
installer\APACDigital.iss
```

Depois de executar `.\scripts\publish.ps1`, abra esse arquivo no Inno Setup e compile para gerar um `Setup.exe`.

## Arquivos importantes

- `Program.cs`: tela principal do aplicativo.
- `Models/DadosApac.cs`: dados usados para preencher o laudo.
- `Services/GeradorPdfService.cs`: preenchimento do PDF via iText.
- `Templates/Laudo de APAC - Modelo.pdf`: modelo PDF preenchivel.
- `cids.txt`: lista local de CIDs usada na busca automatica.

## Observacao de licenca

O projeto usa iText, que exige atencao a licenca AGPL/comercial. Antes de distribuir publicamente, confirme se o uso pretendido esta de acordo com a licenca aplicavel.
