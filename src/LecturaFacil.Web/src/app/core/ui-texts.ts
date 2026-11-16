// SPDX-License-Identifier: Apache-2.0
// Todos los textos de la interfaz, en español claro, en un único sitio.
import { DocumentStatus } from './documents/document.models';

export const APP_TEXTS = {
  name: 'Lectura Fácil IA',
  skipToContent: 'Saltar al contenido',
  mainNavigation: 'Navegación principal',
  navNew: 'Nuevo documento',
  navList: 'Mis documentos',
  draftNotice:
    'Cada adaptación es un borrador. Siempre la tiene que revisar y validar una persona antes de usarla.',
} as const;

export const UPLOAD_TEXTS = {
  pageTitle: 'Nuevo documento',
  intro: 'Pega un texto o sube un PDF con texto. Después pulsa «Adaptar».',
  modeLabel: 'Qué quieres adaptar',
  modeText: 'Pegar texto',
  modePdf: 'Subir PDF',
  titleLabel: 'Título (opcional)',
  textLabel: 'Texto que quieres adaptar',
  fileLabel: 'Fichero PDF',
  dropHint: 'Arrastra aquí el PDF o elige un fichero. Tamaño máximo: 5 MB.',
  selectedFile: 'Fichero elegido:',
  submit: 'Adaptar',
  submitting: 'Enviando…',
  textRequired: 'Escribe o pega el texto que quieres adaptar.',
  fileRequired: 'Elige un fichero PDF.',
  fileTooLarge: 'El fichero pesa más de 5 MB. Prueba con un PDF más pequeño.',
  fileNotPdf: 'El fichero no es un PDF. Solo se admiten ficheros PDF con texto.',
  fileEmpty: 'El fichero está vacío.',
} as const;

export const PROGRESS_TEXTS = {
  title: 'Progreso',
  steps: {
    received: 'Recibido',
    adapting: 'Adaptando',
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
} as const;

export const LIST_TEXTS = {
  pageTitle: 'Mis documentos',
  caption: 'Documentos que has enviado, del más nuevo al más antiguo',
  loading: 'Cargando documentos…',
  empty: 'Todavía no tienes documentos.',
  createFirst: 'Adaptar un documento',
  columnTitle: 'Título',
  columnDate: 'Fecha',
  columnStatus: 'Estado',
  columnProvider: 'Proveedor',
  noProvider: 'Sin adaptar todavía',
  retry: 'Reintentar',
} as const;

export const DOCUMENT_TEXTS = {
  loading: 'Cargando documento…',
  statusLabel: 'Estado:',
  adaptationComing:
    'Aquí verás el texto original y la adaptación lado a lado. Esta vista estará disponible próximamente.',
  adaptationFailed:
    'No se ha podido adaptar el documento. Vuelve a enviarlo desde «Nuevo documento».',
  backToList: 'Volver a Mis documentos',
  retry: 'Reintentar',
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
  notFound: 'No se ha encontrado el documento.',
  fileTooLarge: 'El fichero es demasiado grande. Prueba con un PDF más pequeño.',
  unsupported: 'Ese tipo de contenido no se puede adaptar. Pega un texto o sube un PDF.',
  unprocessable:
    'No se ha podido leer el PDF. Comprueba que tiene texto y que no está dañado ni protegido.',
  tooManyRequests:
    'Has enviado muchos documentos seguidos. Espera un minuto y vuelve a intentarlo.',
  unavailable: 'El servicio está ocupado ahora mismo. Inténtalo de nuevo en unos minutos.',
  invalidFileType: 'El fichero no es un PDF. Solo se admiten ficheros PDF con texto.',
  scannedPdf: 'Este PDF parece escaneado. De momento solo se admiten PDF con texto.',
  unreadablePdf:
    'No se ha podido leer el PDF. Comprueba que no está dañado ni protegido con contraseña.',
  pdfTooComplex: 'El PDF es demasiado largo. Prueba con un documento más corto.',
  textTooShort: 'El texto es demasiado corto para adaptarlo. Escribe al menos unas frases.',
  textTooLong: 'El texto es demasiado largo. Prueba a adaptarlo por partes.',
  progressLost: 'Se ha perdido la conexión con el servidor y no se puede seguir el progreso.',
} as const;
