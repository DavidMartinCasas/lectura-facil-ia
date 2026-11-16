# Pautas de lectura fácil que se detectan

El detector de problemas (US-05) comprueba el texto original con **reglas deterministas en código**, sin LLM. Están en
`src/LecturaFacil.Domain/Guidelines/Rules/`, una clase por pauta, y las aplica `GuidelineAnalyzer`. Son las mismas
reglas que usará el revisor de pautas de la adaptación (US-07).

Las pautas se inspiran en la norma UNE 153101 EX y en la sección 10 de [`TFM-Lectura-Facil.md`](TFM-Lectura-Facil.md).
El catálogo (código, descripción y gravedad por defecto) está en `GuidelineCatalog` y se siembra en la tabla `guidelines`.

## Tabla de pautas

| Código | Pauta | Gravedad | Cómo se detecta |
|---|---|---|---|
| `FRASES_CORTAS` | Frases cortas | Media | La frase tiene más palabras que `Limits:MaxWordsPerSentence` (15 por defecto). Cuenta como palabra cualquier secuencia de letras o cifras ("2,5" y "7/2024" son una palabra; "%" no) |
| `UNA_IDEA_POR_FRASE` | Una idea por frase | Baja | Aproximación sin análisis sintáctico: la frase tiene más conectores que abren otra oración que `Limits:MaxConnectorsPerSentence` (1 por defecto). Conectores: "cuyo", "siempre que", "aunque", "porque", "ya que", "sin embargo", "el cual", "cuando", "donde"… "y", "o" y "si" no cuentan. **Se aparta de la sección 10**: no cuenta verbos conjugados (haría falta un analizador morfológico) ni "y"/"o", que en los textos administrativos unen sobre todo palabras y darían muchos falsos positivos |
| `SIN_SIGLAS` | Sin siglas sin explicar | Alta | Palabras de 2 a 6 mayúsculas que no están en la lista de siglas permitidas ni son números romanos. Si la sigla aparece alguna vez entre paréntesis o seguida de un paréntesis ("Boletín Oficial de la Provincia (BOP)" o "BOP (Boletín…)"), se da por explicada. Se ignoran los títulos escritos en mayúsculas ("BASES DE LA CONVOCATORIA"): frases con más de la mitad de las palabras en mayúsculas |
| `SIN_PORCENTAJES` | Sin porcentajes | Media | "40 %", "40%", "12,5 %", "40 por ciento", "cuarenta por ciento", "100 por cien", "40 por 100" |
| `NUMEROS_EN_CIFRAS` | Números en cifras | Baja | Números escritos con letras ("quince", "treinta y dos", "dos mil"). No cuentan "un", "uno" ni "una" (casi siempre son artículos) ni el "cien" o "ciento" de "por ciento" (sí "por dos", "por tres meses") |
| `SIN_VOZ_PASIVA` | Sin voz pasiva | Media | "ser/estar + participio" ("será revisada", "sido aprobada", "está previsto"), pasiva refleja con "se" en futuro o presente plural ("se publicará", "se publican", salvo pronominales como "se encuentran"), en presente singular solo con verbos administrativos ("se concede", "se publica", "se abona"…) y con el pronombre al final tras un verbo de obligación o posibilidad ("deberán presentarse") |
| `SIN_NEGACIONES_DOBLES` | Sin negaciones dobles | Alta | "no" seguido, en la misma frase, de "nunca", "nada", "nadie", "ningún/ninguno/a/os/as", "tampoco" o "jamás" |
| `SIN_NUMEROS_ROMANOS` | Sin números romanos | Media | Números romanos bien formados en mayúsculas ("IV", "XII", "MMXXVI"). De una sola letra solo cuentan I, V y X: C, D, L y M sueltas suelen ser abreviaturas ("D. Juan", "calle C") |
| `PALABRAS_DIFICILES` | Palabras difíciles explicadas | Media | Palabras de una lista inicial, pequeña y ampliable, con su alternativa ("sufragar" → "pagar", "arrendamiento" → "alquiler", "días hábiles" → "días de lunes a viernes que no son festivos") |
| `SIN_EXPRESIONES_JURIDICAS` | Sin expresiones jurídicas complejas | Alta | Expresiones de una lista configurable con su alternativa ("en virtud de" → "según", "a contar desde" → "desde"). Si dos se solapan, gana la más larga |
| `FECHAS_COMPLETAS` | Fechas completas | Baja | Fechas en cifras: `dd/mm/aaaa`, `d-m-aa`, `dd.mm.aaaa` y `aaaa-mm-dd`, con día y mes válidos. Con puntos se exige el año con cuatro cifras para no confundir una fecha con un apartado ("3.2.10"). Se sugiere la fecha completa ("15 de diciembre de 2026") |
| `TITULOS_CLAROS` | Títulos claros | Baja | **Solo en la adaptación** (US-07): necesita saber qué líneas son títulos. Cada sección tiene siempre una pregunta que hace de título (lo exige el esquema); la regla avisa si el título tiene más palabras que `Limits:MaxWordsPerSentence`. Está en `ClearHeadingsRule` y no implementa `IGuidelineRule` porque trabaja sobre la estructura, no sobre el texto |

### Pautas de la sección 10 que no detecta el código (todavía)

| Pauta | Motivo |
|---|---|
| Símbolos ("€", "%") en lugar de palabras | No está en la sección 10. El "%" lo cubre `SIN_PORCENTAJES`; el resto queda para una regla futura |
| Palabras fuera del vocabulario frecuente | De momento se usa una lista de palabras difíciles conocidas; el vocabulario frecuente completo está fuera del alcance de US-05 |

## Hallazgos

Cada hallazgo guarda **pauta**, **fragmento**, **posición de inicio y de fin**, **gravedad** y **sugerencia** (tabla
`findings`, unida al documento). Se guardan como máximo `Limits:MaxFindingsPerDocument` hallazgos por documento
(500 por defecto, los primeros del texto) para acotar las filas que puede generar una sola petición. Las posiciones son índices de carácter (UTF-16) sobre el **texto original normalizado**
que se guarda en `documents.original_text`, con el fin excluido: `texto[inicio..fin] == fragmento`.

## Frases

`SentenceSegmenter` separa las frases tras `.`, `!`, `?` o `…` seguidos de un espacio y de algo que no empieza en
minúscula, y en cada salto de línea (títulos y elementos de lista son frases). No corta tras abreviaturas comunes
("Sr.", "Sra.", "art.", "núm.", "pág.", "D.", "Dña.", "Avda."…) ni tras iniciales ("J. García"), salvo que la letra
suelta numere algo ("capítulo V.", "Anexo I.").

## Listas configurables

Están en `src/LecturaFacil.Infrastructure/Guidelines/Lists/` como JSON embebido en el ensamblado. Para ampliarlas basta
con editar el fichero y volver a compilar:

| Fichero | Contenido |
|---|---|
| `allowed-acronyms.json` | Siglas que no hace falta explicar ("OK", "TV"…) |
| `legal-jargon.json` | Expresiones jurídicas y su alternativa sencilla (`term`, `alternative`; la alternativa puede ir vacía) |
| `difficult-words.json` | Palabras difíciles y su alternativa. Se buscan formas exactas: cada forma (plural, femenino, tiempo verbal) es una entrada |

## Límites de las reglas

Son pistas para un borrador. **Que no haya hallazgos no significa que el texto sea lectura fácil**: la validación con
personas lectoras sigue siendo obligatoria.

## Revisor de pautas de la adaptación (US-07)

Después de adaptar, `ReviewAdaptation` aplica **las mismas reglas** (`IGuidelineChecker`) al texto adaptado y añade
`TITULOS_CLAROS`. El resultado es un `GuidelineReport` que se guarda con la versión (columna `guideline_report`,
`jsonb`, de `adapted_versions`) y que devuelve `GET /api/documents/{id}` en `adaptation.guidelines`:

- **Una entrada por pauta del catálogo**, con su estado: `Compliant`, `NotCompliant` o `NotChecked` (una búsqueda agotó
  su tiempo; no se da por cumplida). El resumen dice, por ejemplo, "11 de 12 pautas cumplidas".
- **Texto revisado:** el título, y cada pregunta y cada frase en su propia línea (separadas por `\n`), sin el glosario.
  Las posiciones de los hallazgos se refieren a ese texto (`AdaptedContent.PlainText`).
- **Glosario:** una sigla o una palabra difícil que aparece en un término del glosario, como palabra completa y sin
  distinguir mayúsculas, cuenta como explicada ("BOP" la explican "BOP" y "Boletín Oficial de la Provincia (BOP)", pero
  no "BOPA"). Solo afecta a `SIN_SIGLAS` y `PALABRAS_DIFICILES`.
- **Recuentos y límite:** cada pauta guarda su número real de hallazgos, pero solo se guardan los primeros
  `Limits:MaxFindingsPerDocument` hallazgos del texto.
- **Reintento:** si la adaptación tiene hallazgos de gravedad **alta**, se pide otra adaptación del documento completo
  (como mucho `Limits:MaxAdaptationRetries`, 1 por defecto; 0 lo desactiva). Al prompt del adaptador se le añade
  `adapter-retry.v1` con las pautas incumplidas y su número de casos: solo descripciones fijas del catálogo, nunca
  fragmentos del documento ni de la respuesta del modelo. Se guarda **la mejor versión** (`BestVersionPolicy`): la que
  tiene menos hallazgos de gravedad alta y, si empatan, menos hallazgos en total; si siguen empatadas, la primera. Si el
  reintento falla, se guarda la primera. La versión guardada indica el prompt que la generó (`adapter.v1` o
  `adapter.v1+adapter-retry.v1`) y suma los tokens y el tiempo de todos los intentos.

## Seguridad y rendimiento

- Todas las expresiones regulares tienen un **tiempo máximo por búsqueda** (250 ms). Si un texto patológico lo agota,
  esa pauta queda sin comprobar, el análisis sigue con las demás y se registra un aviso con el código de la pauta y la
  longitud del texto (nunca su contenido).
- Las expresiones están escritas sin cuantificadores anidados ambiguos y el segmentador recorre el texto una sola vez:
  20 000 caracteres se analizan en menos de 500 ms (hay un test que lo comprueba).

## Añadir una pauta

1. Añade su código en `GuidelineCodes` y su entrada en `GuidelineCatalog` (y una migración para sembrarla).
2. Crea una clase que implemente `IGuidelineRule` en `Domain/Guidelines/Rules`, con sus tests.
3. Regístrala en `GuidelineRules.Create`. El analizador no cambia.
4. Añade una fila a la tabla de este documento.
