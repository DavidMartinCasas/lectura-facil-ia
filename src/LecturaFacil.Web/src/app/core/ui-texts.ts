// SPDX-License-Identifier: Apache-2.0
// Todos los textos de la interfaz, en español claro, en un único sitio.
import {
  DocumentStatus,
  ExportFormat,
  GuidelineStatus,
  KeyFactStatus,
  KeyFactType,
  Severity,
} from './documents/document.models';

export const APP_TEXTS = {
  name: 'Lectura Fácil IA',
  skipToContent: 'Saltar al contenido',
  mainNavigation: 'Navegación principal',
  navList: 'Mis documentos',
  navSamples: 'Ejemplos',
  noticeLabel: 'Aviso',
  navLogin: 'Entrar',
  logout: 'Salir',
  logoutFailed: 'No se ha podido cerrar la sesión. Inténtalo de nuevo.',
  signedInAs: 'Has entrado como',
  theme: 'Tema',
  themeSystem: 'Sistema',
  themeLight: 'Claro',
  themeDark: 'Oscuro',
  /** Botón de tema: cada pulsación pasa al tema siguiente. */
  themeChange: 'Cambiar el tema.',
  draftNotice:
    'Cada adaptación es un borrador. Siempre la tiene que revisar y validar una persona antes de usarla.',
} as const;

export const LOGIN_TEXTS = {
  pageTitle: 'Entrar',
  emailLabel: 'Correo',
  passwordLabel: 'Contraseña',
  submit: 'Entrar',
  submitting: 'Entrando…',
  required: 'Escribe tu correo y tu contraseña.',
} as const;

export const UPLOAD_TEXTS = {
  pageTitle: 'Nuevo documento',
  intro: 'Pega un texto o sube un PDF con texto. Después pulsa «Adaptar».',
  modeLabel: 'Qué quieres adaptar',
  modeText: 'Pegar texto',
  modePdf: 'Subir PDF',
  titleLabel: 'Título (opcional)',
  titlePlaceholder: 'Sin título',
  textLabel: 'Texto que quieres adaptar',
  textPlaceholder: 'Pega aquí la convocatoria, las bases o la notificación…',
  words: (count: number) => (count === 1 ? '1 palabra' : `${count.toLocaleString('es')} palabras`),
  fileLabel: 'Fichero PDF',
  dropTitle: 'Arrastra aquí el PDF',
  dropHint:
    'O elígelo desde tu equipo. Tiene que tener texto, no solo imágenes. Tamaño máximo: 5 MB.',
  selectedFile: 'Fichero elegido:',
  removeFile: 'Quitar',
  trySample: '¿No tienes un texto a mano?',
  trySampleLink: 'Prueba con un ejemplo',
  submit: 'Adaptar',
  submitting: 'Enviando…',
  textRequired: 'Escribe o pega el texto que quieres adaptar.',
  fileRequired: 'Elige un fichero PDF.',
  fileTooLarge: 'El fichero pesa más de 5 MB. Prueba con un PDF más pequeño.',
  fileNotPdf: 'El fichero no es un PDF. Solo se admiten ficheros PDF con texto.',
  fileEmpty: 'El fichero está vacío.',
} as const;

export const TURNSTILE_TEXTS = {
  hint: 'Antes de enviar, confirma que eres una persona.',
  required: 'Completa la verificación de que eres una persona antes de adaptar.',
  loadFailed:
    'No se ha podido cargar la verificación. Recarga la página o desactiva el bloqueador de contenido.',
} as const;

export const SAMPLES_TEXTS = {
  pageTitle: 'Ejemplos',
  intro:
    'Prueba la herramienta con uno de estos textos. Son ficticios y no tienen datos personales.',
  loading: 'Cargando ejemplos…',
  loadFailed: 'No se han podido cargar los ejemplos.',
  retry: 'Reintentar',
  source: 'Fuente:',
  showText: 'Ver el texto',
  adapt: 'Adaptar este ejemplo',
  adaptLabel: (title: string) => `Adaptar el ejemplo «${title}»`,
  submitting: 'Enviando…',
} as const;

export const PROGRESS_TEXTS = {
  title: 'Progreso',
  steps: {
    received: 'Recibido',
    adapting: 'Adaptando',
    adaptingAgain: 'Adaptando otra vez',
    reviewing: 'Revisando pautas',
    verifying: 'Verificando',
    ready: 'Listo',
  },
  stateDone: 'hecho',
  stateCurrent: 'en curso',
  statePending: 'pendiente',
  stateFailed: 'con error',
  announceCurrent: 'Paso actual:',
  announceReady: 'El documento está listo.',
  announceFailed: 'No se ha podido terminar.',
  adaptationFailed: 'No se ha podido adaptar el documento.',
  retry: 'Reintentar',
  startOver: 'Empezar de nuevo',
  workingTitle: 'Adaptando tu documento',
  workingHint: 'Suele tardar menos de un minuto. Puedes quedarte en esta página.',
  readyTitle: 'Documento listo',
  readyHint: 'Ahora te llevamos a revisarlo.',
  failedTitle: 'No se ha podido terminar',
  openDocument: 'Ver el documento',
} as const;

export const LIST_TEXTS = {
  pageTitle: 'Mis documentos',
  newDocument: 'Nuevo documento',
  caption: 'Documentos que has enviado, del más nuevo al más antiguo',
  loading: 'Cargando documentos…',
  empty: 'Todavía no tienes documentos.',
  createFirst: 'Adaptar un documento',
  columnTitle: 'Título',
  columnStatus: 'Estado',
  columnWarnings: 'Revisión',
  columnReadability: 'Legibilidad',
  columnUpdated: 'Modificado',
  columnProvider: 'Proveedor',
  noProvider: 'Sin adaptar todavía',
  notAdapted: 'Sin adaptar todavía',
  pendingKeyFacts: (count: number): string =>
    count === 1 ? '1 dato por revisar' : `${count} datos por revisar`,
  keyFactsReviewed: 'Revisado',
  brokenGuidelines: (count: number): string =>
    count === 1 ? '1 pauta sin cumplir' : `${count} pautas sin cumplir`,
  readabilityChange: (original: number, adapted: number, grade: string): string =>
    `De ${original} a ${adapted} puntos: ${grade}`,
  justNow: 'Hace un momento',
  updatedAt: (when: string): string => `Modificado ${when.charAt(0).toLowerCase()}${when.slice(1)}`,
  retry: 'Reintentar',
  searchLabel: 'Buscar por título',
  statusFilterLabel: 'Estado',
  clearFilters: 'Quitar filtros',
  noMatches: 'Ningún documento coincide con la búsqueda.',
  noMatchesHint: 'Prueba con otras palabras o quita algún estado.',
  loadMore: 'Cargar más documentos',
  loadingMore: 'Cargando más documentos…',
  updating: 'Actualizando la lista…',
  shown: (count: number, more: boolean): string =>
    `${count === 1 ? 'Se muestra 1 documento' : `Se muestran ${count} documentos`}${more ? '; hay más.' : '.'}`,
} as const;

export const DOCUMENT_TEXTS = {
  loading: 'Cargando documento…',
  statusLabel: 'Estado:',
  adaptationFailed:
    'No se ha podido adaptar el documento. Vuelve a enviarlo desde «Nuevo documento».',
  retriableFailure: 'No se ha podido adaptar el documento. Puedes volver a intentarlo.',
  retryAdaptation: 'Reintentar la adaptación',
  retryingAdaptation: 'Reintentando…',
  retriedAnnouncement: 'Adaptando de nuevo el documento.',
  backToList: 'Volver a Mis documentos',
  retry: 'Reintentar',
} as const;

export const REVIEW_TEXTS = {
  draftLabel: 'Borrador · pendiente de validación',
  processedBy: 'Procesado por:',
  version: 'Versión',
  originalTitle: 'Texto original',
  originalHint:
    'Los fragmentos marcados incumplen alguna pauta. Selecciónalos para ver cuál y qué hacer.',
  noOriginalFindings: 'No se ha detectado ningún problema en el texto original.',
  problemsIn: 'problemas de lectura fácil en',
  selectedTitle: 'Fragmento seleccionado',
  suggestion: 'Sugerencia:',
  severity: 'Gravedad:',
  closeSelection: 'Cerrar',
  adaptedTitle: 'Adaptación',
  tabsLabel: 'Original y adaptación',
  glossaryTitle: 'Glosario',
  editSection: 'Editar sección',
  questionLabel: 'Pregunta',
  sentencesLabel: 'Frases (una por línea)',
  sentencesHint:
    'Escribe cada frase en su propia línea. Al guardar se vuelven a revisar las pautas y la legibilidad.',
  save: 'Guardar',
  saving: 'Guardando…',
  cancel: 'Cancelar',
  emptySection: 'La sección necesita una pregunta y al menos una frase.',
  guidelinesTitle: 'Pautas',
  guidelinesUnavailable: 'Esta versión no tiene informe de pautas.',
  readabilityTitle: 'Legibilidad (INFLESZ)',
  readabilityBefore: 'Original',
  readabilityAfter: 'Adaptación',
  readabilityHint: 'Es un valor aproximado. Cuanto más alto, más fácil de leer.',
  readabilityUnavailable: 'No se ha podido calcular la legibilidad.',
  improvement: 'Diferencia:',
  points: 'puntos',
  keyFactsTitle: 'Datos clave',
  keyFactsHint:
    'Los avisos se quedan a la vista. Márcalos como revisados cuando los hayas comprobado.',
  noKeyFacts: 'No hay datos clave que comprobar.',
  notVerified: 'No se ha podido verificar el significado: revisa las cifras con el original.',
  original: 'Original:',
  adapted: 'En la adaptación:',
  notInAdaptation: 'No aparece',
  acknowledge: 'Marcar como revisado',
  acknowledged: 'Revisado',
  exportBlocked: (count: number) =>
    count === 1
      ? 'Para exportar, marca como revisado el dato clave con aviso.'
      : `Para exportar, marca como revisados los ${count} datos clave con aviso.`,
  exportReady: 'Todos los avisos de datos clave están revisados.',
  exportTitle: 'Exportar',
  exportFormatLabel: 'Formato',
  exportHint:
    'El fichero lleva la fecha de hoy y su estado: borrador pendiente de validación o versión validada.',
  exportButton: 'Exportar',
  exporting: 'Exportando…',
  exported: 'Fichero exportado. Revisa tus descargas.',
  saved: 'Cambios guardados. Se han vuelto a revisar las pautas y la legibilidad.',
  acknowledgedAnnouncement: 'Dato marcado como revisado.',
  acknowledgeAll: (count: number) => `Marcar los ${count} como revisados`,
  acknowledgeAllConfirm: (count: number) =>
    `Vas a marcar como revisados ${count} datos clave con aviso. Hazlo solo si los has comprobado en el texto original.`,
  acknowledgeAllConfirmButton: (count: number) => `Marcar los ${count}`,
  acknowledgedAllAnnouncement: 'Todos los datos clave con aviso, marcados como revisados.',
  locate: 'Ver en el original',
  locateOff: 'Quitar la marca',
  reviewLabel: 'Revisión',
  closeReview: 'Cerrar la revisión',
  noWarnings: 'Sin avisos',
  warnings: (count: number) => (count === 1 ? '1 aviso' : `${count} avisos`),
  /** Qué queda por mirar, para el botón de la revisión: "1 dato", "2 pautas" o "1 dato y 2 pautas". */
  reviewSummary: (keyFacts: number, guidelines: number): string =>
    [
      keyFacts === 0 ? '' : keyFacts === 1 ? '1 dato' : `${keyFacts} datos`,
      guidelines === 0 ? '' : guidelines === 1 ? '1 pauta' : `${guidelines} pautas`,
    ]
      .filter(Boolean)
      .join(' y '),
  notChecked: 'Sin comprobar',
  guidelinesOf: (compliant: number, total: number) => `${compliant} de ${total}`,
  noReport: 'Sin informe',
} as const;

export const GUIDELINE_STATUS_LABELS: Readonly<Record<GuidelineStatus, string>> = {
  Compliant: 'Cumple',
  NotCompliant: 'No cumple',
  NotChecked: 'Sin comprobar',
};

export const SEVERITY_LABELS: Readonly<Record<Severity, string>> = {
  High: 'alta',
  Medium: 'media',
  Low: 'baja',
};

export const KEY_FACT_STATUS_LABELS: Readonly<Record<KeyFactStatus, string>> = {
  Conserved: 'Conservado',
  Review: 'Revisar',
  Omitted: 'Omitido',
};

export const EXPORT_FORMAT_LABELS: Readonly<Record<ExportFormat, string>> = {
  docx: 'DOCX (Word o LibreOffice)',
  pdf: 'PDF',
  md: 'Markdown',
  txt: 'Texto',
};

export const KEY_FACT_TYPE_LABELS: Readonly<Record<KeyFactType, string>> = {
  Deadline: 'Plazo',
  Amount: 'Importe',
  Requirement: 'Requisito',
  Date: 'Fecha',
  Place: 'Lugar',
  Document: 'Documento',
  Reference: 'Referencia',
  Figure: 'Cifra',
  Other: 'Otro dato',
};

/** Validación humana: la hacen personas del grupo destinatario fuera de la aplicación; aquí se anota. */
export const VALIDATION_TEXTS = {
  title: 'Validación',
  draftHint:
    'Cuando hayas revisado el borrador, envíalo a validación. Personas del grupo destinatario tienen que leerlo y entenderlo.',
  send: 'Enviar a validación',
  sending: 'Enviando…',
  sendBlocked: 'Antes de enviarlo, marca como revisados los avisos de los datos clave.',
  sendUnchanged: 'Cambia el texto antes de volver a enviarlo.',
  changesRequested: (date: string, by: string) => `El ${date}, ${by} pidió cambios.`,
  pendingHint:
    'Exporta el documento y llévalo al grupo de validación. Cuando lo hayan leído, anota aquí lo que han decidido. Mientras tanto no se puede editar.',
  outcomeLegend: '¿El grupo lo entiende?',
  outcomes: {
    Approved: 'Sí, lo entiende',
    ChangesRequested: 'No, pide cambios',
  },
  validatedByLabel: 'Quién lo ha validado',
  validatedByHint:
    'Por ejemplo, «Grupo de lectura de la asociación». No escribas nombres de personas.',
  participantsLabel: 'Personas',
  participantsHint: 'Cuántas personas del grupo lo han leído.',
  participantsRequired: 'Escribe cuántas personas lo han leído, de 1 a 99.',
  hardPartsLegend: '¿Qué partes costó entender? (opcional)',
  hardPartsHint: 'Las verás marcadas en la adaptación para saber dónde cambiarla.',
  glossaryPart: 'Glosario',
  hardPartsTitle: 'Costó entender (lo verás marcado en la adaptación):',
  hardMark: 'Costó entender',
  people: (count: number | null) =>
    count === null ? '' : count === 1 ? ' (1 persona)' : ` (${count} personas)`,
  roundsTitle: 'Rondas de validación',
  roundOutcomes: {
    Approved: 'Lo entiende',
    ChangesRequested: 'Pidió cambios',
  },
  roundLine: (round: number, version: number, date: string, by: string, hardParts: number) =>
    `Ronda ${round}: versión ${version}, ${date}, ${by}` +
    (hardParts === 0
      ? '.'
      : hardParts === 1
        ? ', 1 parte difícil.'
        : `, ${hardParts} partes difíciles.`),
  notesLabel: 'Observaciones (opcional)',
  notesHint: 'Qué ha costado entender o qué hay que cambiar.',
  outcomeRequired: 'Elige si el grupo lo entiende o pide cambios.',
  validatedByRequired: 'Escribe quién lo ha validado.',
  record: 'Guardar el resultado',
  recording: 'Guardando…',
  withdraw: 'Volver a borrador',
  validated: (date: string, by: string) => `Validado el ${date} por ${by}.`,
  publishHint: 'Al publicarlo, esta versión queda como la final y ya no se puede cambiar.',
  publish: 'Publicar',
  publishing: 'Publicando…',
  published: 'Publicado. Esta es la versión final.',
  notes: 'Observaciones:',
  sentAnnouncement: 'Documento enviado a validación.',
  approvedAnnouncement: 'Resultado guardado: el documento está validado.',
  changesAnnouncement: 'Resultado guardado: el documento vuelve a borrador para hacer los cambios.',
  withdrawnAnnouncement: 'El documento vuelve a ser un borrador.',
  publishedAnnouncement: 'Documento publicado.',
} as const;

export const STATUS_LABELS: Readonly<Record<DocumentStatus, string>> = {
  Received: 'Recibido',
  Processing: 'Adaptando',
  Error: 'Con error',
  Draft: 'Borrador',
  PendingValidation: 'Pendiente de validación',
  Validated: 'Validado',
  Published: 'Publicado',
};

/** Mensajes de error para el usuario. Nunca se muestra el texto que devuelve el servidor. */
export const ERROR_TEXTS = {
  offline: 'No se puede conectar con el servidor. Comprueba tu conexión e inténtalo de nuevo.',
  generic: 'Algo ha fallado. Inténtalo de nuevo dentro de un rato.',
  invalidRequest: 'Revisa los datos que has enviado e inténtalo de nuevo.',
  invalidCredentials: 'Correo o contraseña incorrectos.',
  sessionExpired: 'Tu sesión ha caducado. Vuelve a entrar.',
  pageExpired: 'La página ha caducado. Recárgala e inténtalo de nuevo.',
  notFound: 'No se ha encontrado el documento.',
  fileTooLarge: 'El fichero es demasiado grande. Prueba con un PDF más pequeño.',
  unsupported: 'Ese tipo de contenido no se puede adaptar. Pega un texto o sube un PDF.',
  unprocessable:
    'No se ha podido leer el PDF. Comprueba que tiene texto y que no está dañado ni protegido.',
  tooManyRequests: 'Has llegado al límite de envíos. Espera un rato y vuelve a intentarlo.',
  humanVerification:
    'No se ha podido comprobar que eres una persona. Completa la verificación y vuelve a intentarlo.',
  unavailable: 'El servicio está ocupado ahora mismo. Inténtalo de nuevo en unos minutos.',
  invalidFileType: 'El fichero no es un PDF. Solo se admiten ficheros PDF con texto.',
  scannedPdf: 'Este PDF parece escaneado. De momento solo se admiten PDF con texto.',
  unreadablePdf:
    'No se ha podido leer el PDF. Comprueba que no está dañado ni protegido con contraseña.',
  pdfTooComplex: 'El PDF es demasiado largo. Prueba con un documento más corto.',
  textTooShort: 'El texto es demasiado corto para adaptarlo. Escribe al menos unas frases.',
  textTooLong: 'El texto es demasiado largo. Prueba a adaptarlo por partes.',
  progressLost: 'Se ha perdido la conexión con el servidor y no se puede seguir el progreso.',
  conflict:
    'El documento ha cambiado mientras lo revisabas. Vuelve a cargarlo para ver la última versión.',
  notEditable: 'Solo se puede modificar un documento que está en borrador.',
  incompleteEdit:
    'La adaptación necesita un título y, en cada sección, una pregunta y al menos una frase.',
  editTooLong: 'La adaptación es demasiado larga. Acorta el texto e inténtalo de nuevo.',
  keyFactNotFound: 'No se ha encontrado ese dato clave. Vuelve a cargar el documento.',
  exportPendingWarnings: 'Revisa los avisos antes de exportar',
  exportUnsupportedFormat: 'Ese formato no se puede exportar. Elige DOCX, PDF, Markdown o texto.',
  exportNothing: 'Este documento todavía no tiene una adaptación que exportar.',
} as const;
