# US-23 · Documentos largos: adaptar y verificar por partes

| Campo | Valor |
|---|---|
| **Épica** | EP-03 · Adaptación |
| **Prioridad** | Should |
| **Puntos** | 5 |
| **Día previsto** | Martes 24 |
| **Depende de** | US-06, US-07, US-11, US-12 |
| **Requiere humano** | No |
| **Issue** | [#43](https://github.com/DavidMartinCasas/lectura-facil-ia/issues/43) (en GitHub se abrió como «US-21») |

## Historia
**Como** técnica que adapta documentos
**quiero** subir documentos largos, de hasta 150 000 caracteres
**para** no tener que trocearlos a mano, y saber por qué parte va la adaptación y qué partes se han podido verificar.

## Contexto para el agente
- La adaptación ya se hacía por partes (`TextChunker`, `MaxCharactersPerAdaptationChunk`), pero el reintento por pautas
  volvía a adaptar el documento entero y la verificación se hacía de una sola vez, con un límite que un documento
  largo superaba siempre.
- El coste se acota por documento (OWASP LLM10): como mucho
  `partes × (1 + MaxAdaptationRetries) + partes` llamadas al modelo (adaptar, reintentar y verificar cada parte).
- Procesar las partes en paralelo queda fuera: el servidor local atiende una petición cada vez.

## Criterios de aceptación
```gherkin
Escenario: Límites por defecto
  Entonces MaxCharacters es 150 000, MaxAdaptationChunksPerDocument es 30 y MaxEditedCharacters es 200 000

Escenario: Un texto que necesitaría demasiadas partes se rechaza al enviarlo
  Dado un texto que se dividiría en más partes que MaxAdaptationChunksPerDocument
  Cuando lo envío
  Entonces la API responde 400 document.text_too_long, "Divídalo en varios documentos", y no lo guarda ni lo encola

Escenario: Solo se reintentan las partes que incumplen pautas graves
  Dado un documento de tres partes en el que solo la segunda incumple una pauta de gravedad alta
  Cuando se adapta
  Entonces solo se vuelve a adaptar la segunda parte y de cada parte se guarda su mejor versión

Escenario: Las pautas graves que aparecen al unir las partes
  Dado un documento cuyas partes no incumplen pautas graves por separado pero el documento unido sí
  Entonces se reintentan todas las partes que aún tienen reintentos

Escenario: Se verifica cada parte por separado
  Dado un documento de varias partes
  Entonces se llama al verificador una vez por parte, con su parte del original y su parte adaptada
  Y las citas del original se comprueban en su parte y las de la adaptación, en la adaptación entera

Escenario: Verificado en parte
  Dado que alguna parte no se ha podido verificar (sin respuesta, respuesta no válida o demasiado larga)
  Entonces el estado de la verificación es PartiallyVerified, sus cifras quedan para revisar
  Y aparece un aviso "Parte N" para revisar esa parte con el original
  Y la pantalla de revisión dice que alguna parte no se ha podido verificar

Escenario: Progreso por partes
  Dado un documento de varias partes que se está adaptando
  Entonces el progreso dice "Adaptando parte 3 de 10" (y "Verificando parte 2 de 10", "Adaptando otra vez la parte 4 de 10")
  Y el lector de pantalla anuncia la parte
  Y un documento de una sola parte no muestra números
```

## Alcance técnico
- **Domain:** `VerificationPart`; `MeaningVerification.CombineParts` (datos sin repetir, manda el veredicto más grave);
  `VerificationStatus.PartiallyVerified`; `Document.EnterStage` con parte y total (`StagePart`, `StageParts`), que se
  borran al salir de `Processing`.
- **Application:** `AdaptDocument` reintenta por partes y verifica por partes; `VerifyMeaning` con `PartToVerify` y
  `MaxCharactersPerVerification` por parte; `SubmitDocument` rechaza con `DocumentErrors.TooManyParts`;
  `DocumentProgress` con `Part` y `Parts`.
- **Infrastructure:** migración `AddProcessingPart` (`processing_part`, `processing_parts`).
- **Api:** el evento SSE `DocumentStatusEvent` lleva `part` y `parts`.
- **Angular:** `PartProgress` en el progreso, detalle del paso en curso, texto «Verificado en parte».
- **Evals:** `CostEstimator` cuenta una verificación por parte.

## Tests requeridos
- [x] Dominio: combinación de partes (una parte igual que antes, parte fallida, datos repetidos) y fases con parte.
- [x] Casos de uso: reintento solo de la parte grave, reintento por el documento unido, verificación por parte,
  verificación en parte, progreso por partes y rechazo de textos con demasiadas partes.
- [x] API: el evento SSE lleva la parte.
- [x] Unitarios de Angular: progreso con parte, anuncio y «Verificado en parte».
- [ ] Prueba manual con un documento de más de 100 000 caracteres contra el modelo real.
