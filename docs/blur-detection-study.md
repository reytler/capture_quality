# Automatic Blur Detection in Mobile Captured Document Images

## Finalidade
Detectar automaticamente desfoque em documentos capturados por celular antes do OCR.

## Problema
Imagens móveis podem conter motion blur, defocus, sombra, rotação, distorção geométrica e iluminação irregular.

## Melhor método identificado
Eigen Analysis / Singular Value Decomposition.

## Fórmula
Bk = soma dos k primeiros autovalores / soma de todos os autovalores.

## Interpretação
Valores altos de Bk indicam maior probabilidade de blur, pois regiões borradas concentram informação nos primeiros autovalores.

## Pipeline
1. Converter imagem para grayscale.
2. Corrigir iluminação com filtro mediano grande.
3. Extrair intensidade, mediana local e magnitude do gradiente.
4. Aplicar K-means com 2 clusters para separar foreground/background.
5. Ignorar background.
6. Dividir foreground em patches.
7. Aplicar SVD por patch.
8. Calcular Bk.
9. Comparar com threshold.
10. Gerar blur map e decisão de aceite/rejeição.

## Parâmetros recomendados
- k = 1
- patch size = 27x27
- threshold = 0.64
- acurácia em contas reais: 90%
- acurácia em dataset sintético: 98.8%

## Regra
Se Bk >= 0.64, o patch é considerado borrado.
Se muitos patches foreground estiverem borrados, rejeitar a imagem.

## Saídas esperadas
- blur_score_global
- blur_ratio
- blur_map
- patches_analisados
- status: ACCEPTED ou REJECTED

## Orientações
- Implemente o algoritmo de detecção de blur baseado em SVD.
- Use algoritmo determinístico usando SVD + threshold.(O artigo já validou que SVD com k=1, patch 27x27 e threshold 0.64 teve bom desempenho para documentos reais.)
- usar grayscale
- separar foreground/background
- aplicar análise por patches
- calcular Bk
- gerar blur map
- retornar score global
- deixar thresholds configuráveis