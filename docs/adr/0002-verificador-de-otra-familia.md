# ADR 0002 · Verificador del significado de otra familia de modelos, con el código por encima

- **Estado:** propuesta (US-11)
- **Fecha:** 2026-11-16

## Contexto

La adaptación a lectura fácil la escribe un LLM, y un LLM puede cambiar o perder un dato al simplificar: convertir "2,5 veces el IPREM" en "1.500 euros", resumir "quince días hábiles" como "10 días" o quitar un requisito. En un documento administrativo eso es grave: la persona que lo lee puede perder una ayuda o presentar la solicitud tarde. La verificación es la red de seguridad del sistema: **ningún dato clave puede cambiar sin generar un aviso** (sección 11 del documento de diseño).

Un segundo LLM puede encontrar esos datos y compararlos, pero también se equivoca (OWASP LLM09). Y si es de la misma familia que el adaptador, tiende a cometer los mismos errores y a dar por buena la paráfrasis que él mismo habría escrito.

## Decisión

1. **El verificador es un rol propio (`LlmRole.Verifier`) con su propio modelo**, configurado en `Llm:Primary:VerifierModel` (y `Llm:Fallback:VerifierModel`). Quien despliega **debe elegir un modelo de otra familia que el adaptador** (por ejemplo, Qwen para adaptar y Mistral para verificar). La aplicación no puede comprobar la familia de un modelo, así que se documenta en `deploy/.env.example` y aquí.
2. **Un solo prompt en dos pasos** (`verify.v1`): extraer los datos clave del original y localizarlos en la adaptación. El original y la adaptación van cada uno entre sus delimitadores con un identificador aleatorio por petición (OWASP LLM01). La respuesta es JSON con esquema; los tipos, estados y motivos son **listas cerradas**, así el modelo no puede escribir motivos libres que se muestren a la persona.
3. **Las cifras y las citas se comprueban con código** (`NumberNormalizer`, `NumericConsistencyCheck` y `QualifierCheck`, en Domain): "quince" → 15, "250 €" → 250, "1.500" → 1500, "2,5" → 2,5, "15 de diciembre de 2026" = "15/12/2026" ("15 de diciembre", sin año, guarda el día y el mes). Para cada dato del verificador:
   - su cita del original debe estar en el original y su cita de la adaptación, en la adaptación (sin distinguir mayúsculas ni espacios): el modelo no puede citar un texto que no existe;
   - cada cifra del original debe estar en la cita adaptada;
   - lo que acompaña a la cifra también debe seguir ahí: la unidad (días, meses, años, euros, porcentaje), el periodo, el máximo o mínimo y si los días son hábiles o naturales.
4. **Regla de combinación: manda el resultado más grave** (conservado < revisar < omitido). El verificador **solo puede añadir avisos**; nunca quita uno del código. Además, cada cifra del original que el verificador no ha listado y que no está en la adaptación se añade como aviso.
5. **Fallo seguro:** si el verificador no responde, su respuesta no cumple el esquema o los textos superan `Limits:MaxCharactersPerVerification`, la verificación queda como `NotVerified` y **todas las cifras del original quedan para revisar**. La adaptación se guarda igualmente como borrador: la verificación informa, no bloquea ni corrige.
6. **Se verifica solo la versión que se guarda** (la mejor tras los reintentos de la US-07), con una llamada por parte y sin reintentos, para acotar el coste (OWASP LLM10). Un documento largo (US-23) se verifica por partes: cada parte del original con su parte adaptada. Las citas del original se buscan en su parte; las de la adaptación, en la adaptación entera. Si alguna parte no se puede verificar, el estado es `PartiallyVerified`: sus cifras quedan para revisar y se añade un aviso "Parte N".

## Consecuencias

- La comprobación por código solo entiende cifras, fechas y algunos calificativos. Los datos sin números (un requisito, un lugar) dependen del verificador, salvo que su cita sea inventada; por eso se le pide "ante la duda, revisar".
- La verificación va del original a la adaptación. Los datos que la adaptación **añade** (un requisito o una cifra que no están en el original) no se detectan todavía; queda como mejora (aviso de "dato añadido").
- Los avisos nunca se descartan por el límite `Limits:MaxKeyFactsPerVersion`, que solo acota los datos conservados. Si un texto anómalo agota el tiempo de las expresiones regulares, la verificación queda como `NotVerified` con un único aviso: "revisar todo el documento".
- La comprobación por código puede dar falsos avisos (por ejemplo, si la adaptación escribe "2.5" en lugar de "2,5", o una cifra del original no importa). Se acepta: un aviso de más lo descarta una persona; uno de menos puede llegar a la ciudadanía.
- Con el respaldo habilitado, el verificador también puede responder desde el respaldo, con su `VerifierModel`. La regla de "otra familia" se aplica a cada proveedor.
- El original y la adaptación de cada parte viajan juntos al verificador: `MaxCharactersPerVerification` se aplica a cada parte, no al documento entero. Si subes `MaxCharactersPerAdaptationChunk`, comprueba que una parte y su adaptación caben en el contexto del modelo verificador.
- Un dato cuyo original queda partido entre dos partes puede salir como aviso; se acepta por la misma razón que los falsos avisos.
