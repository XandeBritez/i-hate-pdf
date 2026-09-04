<div align="center">

<img src="Assets/logo-full.png" alt="i HATE PDF" width="300" />

### O PDF começou. Você termina.

Sete ferramentas de PDF no Windows — **sem upload, sem conta, sem marca d'água**.<br/>
Nenhum arquivo sai da sua máquina.

<br/>

[![Baixar](https://img.shields.io/badge/⬇_Baixar_a_última_versão-E5322D?style=for-the-badge&logoColor=white)](https://github.com/XandeBritez/i-hate-pdf/releases/latest)
&nbsp;
![.NET 8](https://img.shields.io/badge/.NET_8-WPF-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Windows](https://img.shields.io/badge/Windows_10_/_11-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![Offline](https://img.shields.io/badge/100%25_offline-16A34A?style=for-the-badge)

</div>

---

## Por que existe

Todo site de PDF grátis cobra o mesmo pedágio: subir seu contrato para o servidor de um desconhecido, esperar a fila, aceitar a marca d'água e torcer para que o arquivo seja apagado depois.

**i HATE PDF** não tem servidor. É um `.exe`, você arrasta, ele resolve.

<div align="center">
<img src="docs/screenshots/unir.png" alt="Tela de unir PDFs com capas em grade" width="860" />
</div>

---

## As ferramentas

### 🧩 Unir

Arraste vários PDFs. Cada um vira um **card com a capa visível** e um selo com a posição dele no documento final. Arrastou o card, mudou a ordem.

### ✂️ Editar páginas

Abra um PDF e veja **cada página como miniatura**. Marque as que morrem, arraste as que ficam, **gire** as tortas (90° para cada lado), puxe páginas de outro arquivo.

Cada card mostra a **posição nova** e a **original** (`era 3`), então dá para conferir a reorganização antes de gravar. **Salvar seleção** grava só as páginas marcadas num arquivo à parte — é assim que se divide um PDF aqui, sem precisar apagar nada. O original nunca é tocado.

<div align="center">
<img src="docs/screenshots/editar.png" alt="Editor visual de páginas" width="860" />
</div>

### 🗜️ Comprimir

Dois modos, porque não existe um só que sirva para tudo:

- **Otimizar** reescreve o arquivo com os fluxos comprimidos. O texto continua selecionável; o ganho depende de quanto o gerador original desperdiçou.
- **Reduzir imagens** rasteriza cada página em JPEG (200 / 150 / 110 dpi). É o que realmente encolhe digitalizações — mas o documento **deixa de ter texto selecionável**, e o app conta quantos arquivos da fila perderiam isso e avisa antes.

Medido num PDF de 8 páginas cheias de imagem:

| Modo | Antes | Depois | Ganho |
|---|---|---|---|
| Otimizar | 776 KB | 776 KB | — (já otimizado) |
| Reduzir · alta | 776 KB | 184 KB | **76%** |
| Reduzir · equilibrada | 776 KB | 100 KB | **87%** |
| Reduzir · máxima | 776 KB | 60 KB | **92%** |

> Se a cópia não ficar menor que a entrada, o **original é mantido** e o item diz "já estava otimizado" — comprimir nunca devolve um arquivo pior. A saída sai com sufixo `-comprimido`: seu arquivo original nunca é sobrescrito.

<div align="center">
<img src="docs/screenshots/comprimir.png" alt="Tela de compressão" width="860" />
</div>

### 🔁 Converter para PDF

`TXT` e **imagens** (JPG, PNG, WEBP, BMP, GIF, TIFF) são convertidos direto pelo app. `DOCX` e `XLSX` passam pelo **LibreOffice headless**, em fila, com o erro de cada arquivo na mão.

As imagens podem virar **um arquivo cada ou um PDF único**. No modo PDF único a fila vira uma **grade de miniaturas arrastáveis**, com o número da página em cada card — a ordem que você vê é a ordem do documento.

<div align="center">
<img src="docs/screenshots/converter.png" alt="Imagens em grade arrastável" width="860" />
</div>

### 📤 PDF para outros formatos

Um PDF vira `.docx` editável (via Writer do LibreOffice), `.txt` puro (extração nativa com PdfPig) ou **imagens PNG/JPG**, uma por página, numa pasta com o nome do documento. Só o `.docx` depende do LibreOffice.

> **Honestidade sobre o formato:** o PDF descreve *posições de glifos*, não parágrafos. O texto sai bem; layout complexo — colunas, tabelas, caixas — é reconstruído por aproximação. E um PDF **escaneado não tem texto algum**: o app avisa em vez de entregar um arquivo vazio, porque não faz OCR.

<div align="center">
<img src="docs/screenshots/pdf-para-word.png" alt="Tela de exportação" width="860" />
</div>

### 🔐 Senha

Coloca senha de abertura (**AES-256**) ou tira, sempre gerando uma cópia. Confirmação de senha só no modo proteger, onde errar deixaria o arquivo inacessível para sempre.

> **Remover exige saber a senha.** O app não quebra proteção: ele regrava sem criptografia para quem já tem acesso.

<div align="center">
<img src="docs/screenshots/senha.png" alt="Tela de senha" width="860" />
</div>

---

## Fora do app

**No menu do botão direito.** A tela **Sobre** liga as entradas do Explorer: *Editar páginas*, *Comprimir* e *Unir* em arquivos PDF, *Converter para PDF* em imagens. O registro é feito em `HKEY_CURRENT_USER` — **sem UAC** — e o mesmo botão desfaz tudo.

**Ele lembra o que você escolheu.** Tema, pastas de saída de cada tela, modo de compressão, formato de exportação: tudo volta como estava, em `%AppData%\IHatePdf\settings.json`. Fica fora da pasta do app de propósito — a atualização automática troca o executável, e configuração ao lado dele se perderia.

**Tema claro e escuro**, alternados no canto inferior esquerdo.

<div align="center">
<img src="docs/screenshots/tema-escuro.png" alt="O mesmo editor no tema escuro" width="860" />
</div>

---

## Instalar

Cada release publica **dois arquivos** — escolha um:

| Arquivo | Para quem |
|---|---|
| **`IHatePdf-Setup-x.y.z.exe`** | Instalação normal: atalhos no Menu Iniciar e na área de trabalho, entrada em *Adicionar ou remover programas*, desinstalador — e **instala o LibreOffice junto**. |
| `IHatePdf-x.y.z-win-x64.zip` | Portátil: extrai e roda, sem instalar nada. Um único `.exe`. |

Os dois são **self-contained**: não precisa instalar o .NET.

### O instalador

Instala em `%LocalAppData%\Programs\IHatePdf` — **de propósito**, e não em `Program Files`. Em `Program Files` o app não conseguiria se sobrescrever, e a atualização automática pararia de funcionar. Como consequência, o app instala **sem pedir UAC**.

Durante a instalação ele oferece instalar o **LibreOffice**, baixando a versão estável e rodando em modo silencioso. A tarefa só aparece se o LibreOffice ainda não estiver na máquina, e o Windows pede elevação **uma vez** nesse passo.

Se você desmarcar a opção, ou se o download falhar, a instalação do app continua normalmente: o botão **"Baixar e instalar LibreOffice"** continua na tela **PDF para outros formatos** para quando você quiser — inclusive se um dia desinstalar o LibreOffice sem querer.

> O LibreOffice é necessário só para **DOCX/XLSX → PDF** e **PDF → .docx**. Todo o resto — unir, editar, comprimir, senha, imagens, `.txt` — funciona sem ele.

### Atualizar

A tela **Sobre** verifica as releases deste repositório e, se houver versão nova, **atualiza sozinha**: baixa o pacote, se substitui e reabre já na versão nova. Nada de zip para você extrair na mão.

> Um `.exe` em execução não pode sobrescrever a si mesmo, então o app grava um script temporário que espera o processo encerrar, troca o arquivo e reabre. O executável atual vira `.old` antes da cópia e é restaurado se algo falhar no meio. Se o app estiver numa pasta sem permissão de escrita, ele avisa e manda você para a página da release em vez de tentar e falhar.

<div align="center">
<img src="docs/screenshots/sobre.png" alt="Tela Sobre" width="860" />
</div>

---

## Compilar do código

```bash
git clone https://github.com/XandeBritez/i-hate-pdf.git
cd i-hate-pdf
dotnet run
```

Requer o **.NET 8 SDK**. O `csproj` usa `RollForward=LatestMajor`, então runtimes mais novos também servem.

Publicar como os releases fazem:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

---

## Como foi construído

MVVM estrito com `CommunityToolkit.Mvvm` — nenhuma View tem lógica no code-behind, nem o drag & drop.

```
Models/         PageReference · PdfFileItem · PageItem
                ConversionItem · CompressionItem
Services/       PdfService · ConversionService (+converters)
                PdfCompressionService · PdfExportService · PdfSecurityService
                ImageToPdfService · ImageThumbnailService · PdfRenderService
                LibreOfficeRunner · WindowsFontResolver
                SettingsService · ShellIntegrationService · GitHubUpdateService
ViewModels/     Main · Merge · Editor · Compress · Converter
                PdfToWord · Security · About
Views/          MergeView · EditorView · CompressView · ConverterView
                PdfToWordView · SecurityView · AboutView
Behaviors/      FileDropBehavior · PasswordBoxBehavior · conversores
Themes/         Light · Dark · Styles
```

| Peça | Biblioteca | Papel |
|---|---|---|
| Estrutura do PDF | [PDFsharp 6](https://www.nuget.org/packages/PDFsharp) | unir, extrair, remover, reordenar e girar páginas |
| Senha | PDFsharp (`PdfDefaultEncryption.V5`) | AES-256, sem dependência externa |
| Miniaturas e rasterização | [PDFtoImage](https://www.nuget.org/packages/PDFtoImage) (PDFium) | renderiza cada página como bitmap |
| Recompressão de imagem | [SkiaSharp](https://www.nuget.org/packages/SkiaSharp) | decodifica os formatos de entrada e reencoda em JPEG |
| Extração de texto | [PdfPig](https://www.nuget.org/packages/PdfPig) | PDF → TXT e detecção de texto |
| MVVM | [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm) | bindings, comandos, messenger |
| Arrastar cards | [gong-wpf-dragdrop](https://www.nuget.org/packages/gong-wpf-dragdrop) | reordenação dentro das grades |
| DOCX / XLSX | LibreOffice `--headless` | Office → PDF e PDF → Word (`writer_pdf_import`) |
| Instalador | Inno Setup | pacote, atalhos, desinstalador |

**A ideia central:** toda operação de página é a mesma primitiva.

```csharp
// deletar, reordenar, girar, extrair e unir sao a mesma coisa:
// uma lista ordenada de (arquivo, indice de pagina, giro) virando um documento.
await pdfService.BuildAsync(pages.Select(p => p.ToReference()), outputPath);
```

Detalhes que custam caro quando ficam de fora:

- As miniaturas são renderizadas **fora da thread de UI** e chegam com `Freeze()` — sem isso, `InvalidOperationException` no binding.
- O PDFium é serializado por um semáforo; ele não é reentrante.
- O PDFsharp 6 **não resolve fontes sozinho**: sem um `IFontResolver`, converter TXT lança `No appropriate font found`.
- O giro é **somado** ao `/Rotate` que a página já trazia. Substituir viraria a página errada num documento que já vem girado.
- O LibreOffice **não serve para gerar `.txt`** a partir de PDF: ao importar, ele põe o conteúdo em quadros de texto flutuantes, e o filtro de texto puro exporta só o corpo do documento — o arquivo saía **vazio**. Por isso o TXT é extraído com PdfPig.
- O LibreOffice roda com **perfil temporário próprio** (`-env:UserInstallation=`), senão execuções seguidas travam no lock do perfil. E o `soffice` retorna 0 mesmo em algumas falhas, então a saída é validada pela existência do arquivo.
- O `PasswordBox` não expõe `Password` como propriedade bindável. A ponte usa uma propriedade anexada cujo valor padrão é `null` — com `string.Empty`, ela nunca "mudaria" em relação ao ViewModel, o callback não rodaria e o campo aceitaria digitação sem nunca avisar ninguém.
- As miniaturas de imagem usam `DecodePixelWidth` e `CacheOption.OnLoad`: sem o primeiro, uma foto de 12 MP viraria bitmap inteiro na memória; sem o segundo, o arquivo ficaria travado enquanto estivesse na fila.

---

## Limites conhecidos

- Operações de página copiam o conteúdo: **anotações, campos de formulário e sumário (outlines) não são preservados**.
- PDFs protegidos só são lidos pelas outras telas depois de passarem pela tela **Senha**.
- **Não há OCR**: PDF escaneado (imagem pura) não tem texto para extrair.
- Windows apenas. WPF não é multiplataforma.

---

<div align="center">

<img src="Assets/logo-mark.png" alt="" width="72" />

**Feito por Alexandre Britez Borsuka** ([@XandeBritez](https://github.com/XandeBritez)) · [Reportar um problema](https://github.com/XandeBritez/i-hate-pdf/issues)

</div>
