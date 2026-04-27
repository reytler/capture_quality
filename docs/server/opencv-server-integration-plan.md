# Plano de Integracao Server-Side com OpenCV

## Contexto atual da arquitetura

- O fluxo atual de deteccao de blur no backend roda em ASP.NET Core e expoe os endpoints em `src/CaptureQuality.Server/Program.cs`.
- O processamento assincrono de jobs roda em `src/CaptureQuality.Server/Services/BlurJobProcessor.cs`.
- A pipeline principal de deteccao esta hoje concentrada em servicos compartilhados registrados no servidor:
  - `src/CaptureQuality/Services/BlurDetectorService.cs`
  - `src/CaptureQuality/Services/ImageProcessorService.cs`
  - `src/CaptureQuality/Services/SvdAnalyzerService.cs`
  - `src/CaptureQuality/Services/ConfigurationService.cs`
- A implementacao atual usa ImageSharp para carga/manipulacao de imagem e MathNet para SVD.
- Como `src/CaptureQuality/` e um projeto Blazor WebAssembly, dependencias nativas de OpenCV nao devem ser introduzidas diretamente nesse projeto.
- OpenCV.js esta fora de escopo. O foco deste plano e somente OpenCV no servidor ASP.NET Core.

## Objetivo do spike

- Validar a viabilidade tecnica de usar OpenCV no backend sem quebrar os contratos HTTP atuais nem o fluxo de jobs.
- Medir se OpenCV traz ganho real em pre-processamento, segmentacao, gradiente e/ou estabilidade operacional em comparacao com a baseline atual.
- Confirmar o impacto de empacotamento, runtime nativo, observabilidade, consumo de memoria e latencia antes de qualquer migracao mais ampla.

## Desenho alvo

- Manter `src/CaptureQuality.Server/Program.cs` como ponto de composicao de DI e preservar os endpoints existentes.
- Concentrar a integracao com OpenCV apenas no servidor, preferencialmente em novas classes dentro de `src/CaptureQuality.Server/Services/` ou subpasta dedicada, por exemplo `src/CaptureQuality.Server/Services/OpenCv/`.
- Introduzir uma separacao explicita entre:
  - orquestracao da deteccao
  - operacoes de processamento de imagem
  - analise SVD / regra de decisao
- Preservar a API externa do detector; a troca de backend deve acontecer por interface/estrategia e nao por alteracao de contrato.
- Evitar acoplamento do projeto `src/CaptureQuality/` com OpenCV. Se necessario, mover apenas contratos/abstracoes reutilizaveis para uma camada compartilhada futura, mantendo a implementacao OpenCV restrita ao servidor.
- Comecar com OpenCV nas etapas em que ele agrega mais valor pratico: decode, grayscale, resize, median blur, gradiente e apoio a segmentacao.
- Manter a decisao final de blur comparavel a baseline atual, idealmente preservando a logica de SVD e thresholds enquanto o spike estiver em avaliacao.

## Plano em fases

### Fase 1 - Baseline e recorte do spike

- Congelar a baseline atual e registrar metricas do pipeline existente para um conjunto fixo de imagens.
- Mapear quais partes de `src/CaptureQuality/Services/BlurDetectorService.cs` dependem de `ImageProcessorService` e quais podem permanecer iguais.
- Confirmar se o spike vai comparar apenas pre-processamento com OpenCV ou tambem uma eventual troca de partes da segmentacao.
- Definir dataset minimo de comparacao com casos nitidos, motion blur, defocus, baixa iluminacao, sombras e paginas com pouco conteudo.

### Fase 2 - Estrutura server-only

- Adicionar a dependencia .NET escolhida para OpenCV no projeto servidor, nao no projeto Blazor WebAssembly.
- Criar uma abstracao para operacoes de imagem usadas pelo detector, permitindo alternar entre implementacao atual e implementacao OpenCV.
- Ajustar `src/CaptureQuality.Server/Program.cs` para registrar a implementacao ativa por configuracao/feature flag.
- Garantir que `src/CaptureQuality.Server/Services/BlurJobProcessor.cs` continue consumindo o detector sem mudanca de contrato.

### Fase 3 - Integracao minima com OpenCV

- Implementar primeiro o caminho de decode/carregamento da imagem e conversoes basicas.
- Migrar grayscale, resize e median filter para OpenCV mantendo mesmos parametros observaveis sempre que possivel.
- Migrar o calculo de gradiente para OpenCV e verificar compatibilidade numerica com a baseline.
- Avaliar se a segmentacao atual por K-means continua igual ou se deve usar primitivas OpenCV apenas como aceleracao, sem mudar a regra de negocio.

### Fase 4 - Comparacao controlada

- Executar baseline atual e variante OpenCV sobre o mesmo dataset e registrar diferencas por imagem.
- Comparar tempo total, tempo por etapa, memoria, taxa de falha e distribuicao de `blur_ratio`.
- Verificar se os thresholds atuais em `src/CaptureQuality/Services/ConfigurationService.cs` ainda fazem sentido ou se o backend OpenCV muda a escala dos sinais.
- Medir tambem impacto operacional: tamanho de deploy, bibliotecas nativas exigidas, comportamento em ambiente local e de publicacao.

### Fase 5 - Decisao

- Se OpenCV trouxer ganho consistente e custo operacional aceitavel, definir plano de endurecimento e migracao gradual.
- Se o ganho for marginal ou vier com alto custo de manutencao/deploy, manter a baseline atual e limitar OpenCV a um experimento documentado.

## Prioridades OpenCV

- Prioridade 1: carga/decode robusto de imagem e conversao para grayscale.
- Prioridade 2: resize e filtros locais, em especial median blur.
- Prioridade 3: calculo de gradiente/magnitude com primitivas vetorizadas.
- Prioridade 4: apoio a segmentacao foreground/background, desde que sem reescrever cedo a regra inteira.
- Prioridade 5: utilitarios de observabilidade e diagnostico de imagem, se ajudarem a entender divergencias.
- Fora da primeira onda: reimplementar toda a heuristica de blur, trocar imediatamente a SVD atual ou expandir escopo para cliente/browser.

## Validacoes tecnicas obrigatorias

- Validar compatibilidade de pacote/runtime nativo com o target do servidor e com o modo de publish do projeto ASP.NET Core.
- Validar que a integracao nao introduz referencia nativa no projeto `src/CaptureQuality/`.
- Validar comportamento com concorrencia no fluxo de jobs de `src/CaptureQuality.Server/Services/BlurJobProcessor.cs`.
- Validar descarte correto de buffers/objetos nativos para evitar vazamento de memoria.
- Validar cancelamento, timeout e tratamento de erro no caminho sincrono e assincrono.
- Validar thread-safety das chamadas OpenCV usadas no pipeline.
- Validar se o custo de copiar dados entre `Mat`, arrays e estruturas atuais nao elimina o ganho esperado.
- Validar logs e pontos de medicao para diagnosticar divergencias entre baseline e variante OpenCV.

## Criterios de comparacao com baseline

- Mesmo contrato HTTP e mesmas rotas expostas em `src/CaptureQuality.Server/Program.cs`.
- Mesma semantica de resultado: `IsAccepted`, `BlurRatio`, `TotalPatches`, `BlurredPatches`, `Status`, dimensoes e patch size.
- Diferenca controlada de decisao final entre baseline e OpenCV para o dataset de referencia.
- Tempo medio e p95 por imagem.
- Consumo de memoria durante processamento unitario e em carga concorrente.
- Taxa de erro/falha por tipo de imagem.
- Facilidade de deploy e reproducao em ambiente de desenvolvimento e publicacao.
- Necessidade ou nao de recalibrar `K`, `PatchSize`, `PatchThreshold`, `BlurRatioThreshold`, `MedianFilterSize`, `GradientKernelSize` e `MaxImageDimension`.

## Decisoes em aberto

- Qual binding .NET de OpenCV sera usado no spike e qual o custo de distribuicao das libs nativas.
- Se a abstracao deve nascer dentro do detector atual ou em uma camada nova de processamento server-side.
- Se a SVD continua em MathNet na primeira iteracao ou se alguma etapa tambem migra depois.
- Se a segmentacao foreground/background permanece como esta ou recebe uma variante OpenCV opcional.
- Como versionar/configurar feature flag para alternar baseline e OpenCV em ambiente real.
- Qual dataset sera adotado como baseline oficial do time para comparacoes futuras.

## Risco geral e recomendacao final

- O maior risco nao e algoritmico; e arquitetural/operacional: OpenCV e nativo, enquanto a pipeline atual reutiliza servicos no projeto Blazor WebAssembly.
- O melhor caminho e um spike pequeno, isolado e estritamente server-side, sem contaminar `src/CaptureQuality/` com dependencias nativas.
- A recomendacao e usar OpenCV primeiro como backend de operacoes de imagem e manter a regra de decisao o mais proxima possivel da baseline no inicio.
- A decisao de adocao deve depender de evidencia objetiva em performance, robustez e custo de deploy/manutencao, nao apenas de preferencia tecnologica.
- Ate a validacao do spike, a baseline atual continua sendo a referencia funcional do produto.
