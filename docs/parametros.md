# Parâmetros de Aceitação de Imagem

## Conceitos Base

| Termo | Significado |
|---|---|
| `Bk` | Nota de blur de um patch. Quanto maior, mais borrado ele tende a ser |
| `patch borrado` | Patch com `Bk >= PatchThreshold` |
| `blurRatio` | Proporção de patches borrados: `blurredPatches / totalPatches` |
| `foreground` | Região considerada relevante para análise, em geral o documento/conteúdo principal |

## Explicação dos Parâmetros

| Parâmetro | O que é | Efeito prático no resultado | Se aumentar | Se diminuir | Risco principal |
|---|---|---|---|---|---|
| `K` | Quantidade de valores singulares usados no cálculo do `Bk` | Muda a sensibilidade da nota de blur do patch | Tende a rejeitar mais | Tende a aceitar mais | Perder separação entre nítido e borrado |
| `PatchSize` | Tamanho de cada bloco analisado (ex: 27x27) | Define quão local ou grosseira é a análise | Análise mais grossa | Análise mais local/sensível | Grande demais mistura fundo e texto; pequeno demais gera ruído |
| `PatchThreshold` | Limiar para dizer que um patch está borrado | Decide se um patch entra como blur | Tende a aceitar mais | Tende a rejeitar mais | Alto demais deixa passar blur; baixo demais rejeita quase tudo |
| `BlurRatioThreshold` | Limiar global de aceitação da imagem | Define quantos patches borrados são tolerados | Tende a aceitar mais | Tende a rejeitar mais | Fica permissivo ou rígido demais |
| `MedianFilterSize` | Janela do filtro de mediana | Afeta a segmentação de foreground | Suaviza mais | Preserva mais detalhe/ruído | Pode apagar detalhes úteis ou manter ruído excessivo |
| `GradientKernelSize` | Tamanho do kernel de gradiente | Hoje não tem efeito real no código atual | Sem efeito hoje | Sem efeito hoje | Dar falsa impressão de parametrização |
| `MaxImageDimension` | Tamanho máximo antes do resize | Controla quanto detalhe da imagem é preservado | Tende a preservar mais detalhe | Tende a perder mais detalhe | Custo maior ou suavização excessiva |

## Valores Atuais e Direção de Ajuste

| Parâmetro | Valor atual | Papel no sistema | Se quiser aceitar mais | Se quiser rejeitar mais | Observação |
|---|---:|---|---|---|---|
| `K` | `1` | Define quantos valores singulares entram no `Bk` | Manter ou diminuir com cautela | Aumentar | Eu mexeria por último |
| `PatchSize` | `27` | Define o tamanho do bloco analisado | Depende do cenário | Depende do cenário | Não é um ajuste linear; exige teste |
| `PatchThreshold` | `0.64` | Decide se um patch é borrado | Aumentar | Diminuir | Um dos melhores parâmetros para calibrar |
| `BlurRatioThreshold` | `0.35` | Decide se a imagem inteira é aceita | Aumentar | Diminuir | Melhor parâmetro para começar ajuste |
| `MedianFilterSize` | `31` | Afeta a segmentação de foreground | Possivelmente diminuir | Possivelmente aumentar | Efeito indireto; depende da segmentação |
| `GradientKernelSize` | `3` | Em tese controlaria o gradiente | Sem efeito hoje | Sem efeito hoje | No código atual parece inativo |
| `MaxImageDimension` | `1200` | Controla o resize antes da análise | Aumentar | Diminuir | Pode impactar bastante imagens da câmera |

## Sugestão Prática de Ordem para Teste

| Ordem | Parâmetro | Motivo |
|---|---|---|
| 1 | `BlurRatioThreshold` | É o corte final de aceitação; mais fácil de interpretar |
| 2 | `PatchThreshold` | Controla quantos patches viram blur |
| 3 | `MaxImageDimension` | Pode estar suavizando demais a imagem no resize |
| 4 | `MedianFilterSize` | Útil se a segmentação estiver ruim |
| 5 | `PatchSize` | Ajuste mais sensível e menos previsível |
| 6 | `K` | Eu deixaria por último |
| 7 | `GradientKernelSize` | Não vale mexer enquanto não estiver realmente ligado ao cálculo |

## Leitura Rápida

- **Para aceitar mais imagens**: subir `BlurRatioThreshold`, subir `PatchThreshold`, talvez subir `MaxImageDimension`
- **Para rejeitar mais**: fazer o inverso
- **Se o problema for foreground ruim**: revisar `MedianFilterSize` antes de mexer em tudo

## Outras Regras Importantes do Pipeline

- **K-means com `k = 2`**: sempre divide a imagem em dois grupos, foreground e background
- **Foreground = cluster com maior gradiente médio**: assume que a área útil tem mais bordas/detalhes
- Só **patches com algum foreground** entram na conta final
- Se não houver foreground detectado, a imagem sai como aceita com `NO_CONTENT`
- Os patches são **não sobrepostos**, então parte das bordas pode ficar fora da análise

## Onde os Parâmetros Estão Definidos

Todos os thresholds centrais ficam em:
- `src/CaptureQuality/Services/ConfigurationService.cs`

Hoje não há:
- Binding de `appsettings`
- `IOptions`
- Environment variables
- Query params
- UI para alterar esses parâmetros em runtime

O `ConfigurationService` é registrado como singleton puro, sem carregar config externa.

## Diagnóstico: Por que o app pode estar rejeitando tudo?

1. `blurRatio` está frequentemente ficando `>= 0.35` (hipótese mais provável)
2. A segmentação de foreground pode estar incluindo áreas demais
3. O documento pode ocupar pouca parte do frame da câmera
4. O resize para `1200` pode estar degradando a nitidez útil
5. A calibração atual pode estar severa demais para o fluxo real de câmera

## Dados Retornados para Diagnóstico

O backend já devolve no DTO (`BlurDetectionMetricsDto`):
- `IsAccepted`
- `BlurRatio`
- `TotalPatches`
- `BlurredPatches`
- `Status`
- `PatchSize`
- `ImageWidth`
- `ImageHeight`

Esses dados já aparecem na UI via `QualityResult.razor`.

## Arquivos Relacionados

- `src/CaptureQuality/Services/ConfigurationService.cs` - definição dos parâmetros
- `src/CaptureQuality/Services/BlurDetectorService.cs` - regra final de aceitação
- `src/CaptureQuality/Services/SvdAnalyzerService.cs` - cálculo de `Bk` e `blurRatio`
- `src/CaptureQuality/Services/ImageProcessorService.cs` - segmentação de foreground
- `src/CaptureQuality/Components/QualityResult.razor` - exibição dos resultados
