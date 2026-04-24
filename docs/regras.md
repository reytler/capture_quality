1. Objetivo do estudo

Criar um mecanismo automático para detectar desfoque em imagens de documentos capturados por celular, antes do OCR.

O foco é evitar que imagens ruins sejam processadas, principalmente em cenários como:

pagamento de boletos;
leitura de faturas;
extração de campos sensíveis;
OCR de documentos fotografados;
validação de qualidade antes do upload.

2. Problema principal

Imagens capturadas por celular podem ter:

motion blur;
defocus;
sombra;
rotação;
distorção geométrica;
iluminação irregular;
superfície não plana.

O artigo foca especificamente em detecção de blur/desfoque.

3. Solução recomendada pelo estudo

O método mais eficiente foi baseado em Eigen Analysis / Singular Value Decomposition — SVD.

A ideia central:

Regiões borradas perdem informação visual. Por isso, poucos autovalores conseguem representar grande parte da imagem. Já regiões nítidas precisam de mais informação distribuída.

A métrica usada é:
Bk = soma dos k primeiros autovalores / soma de todos os autovalores

Quanto maior o valor de Bk, maior a tendência da região estar borrada.

4. Pipeline recomendado para software
Imagem capturada
   ↓
Converter para escala de cinza
   ↓
Corrigir viés de iluminação com filtro mediano grande
   ↓
Extrair features por pixel/região:
   - intensidade
   - mediana local
   - magnitude do gradiente
   ↓
Segmentar foreground/background com K-means, k=2
   ↓
Ignorar background
   ↓
Dividir foreground em patches
   ↓
Aplicar SVD em cada patch
   ↓
Calcular Bk
   ↓
Comparar com threshold
   ↓
Gerar:
   - blur score global
   - blur map local
   - decisão: aceitar ou rejeitar imagem

5. Parâmetros importantes

Melhor configuração encontrada:

Método: Eigen Analysis / SVD
k = 1
Patch size ideal: 27x27
Threshold recomendado para patch 27: 0.64
Acurácia em dataset sintético: 98.8%
Acurácia em contas/faturas reais: 90%

Tabela prática:

| Patch |  k | Threshold Bk | Observação        |
| ----- | -: | -----------: | ----------------- |
| 17x17 |  1 |         0.63 | bom               |
| 22x22 |  1 |         0.64 | bom               |
| 27x27 |  1 |         0.64 | melhor equilíbrio |
| 32x32 |  1 |         0.63 | bom               |
| 37x37 |  1 |         0.61 | aceitável         |
| 42x42 |  1 |         0.60 | aceitável         |

6. Regra de decisão sugerida
Se Bk >= threshold:
    patch = borrado
Senão:
    patch = nítido

Para decisão global:

blur_ratio = patches_borrados / patches_foreground

Se blur_ratio >= limite_configurado:
    rejeitar imagem
Senão:
    aceitar imagem

Sugestão inicial:

blur_ratio >= 0.35 → imagem ruim
blur_ratio < 0.35 → imagem aceitável

Esse limite deve ser calibrado com imagens reais do seu domínio.