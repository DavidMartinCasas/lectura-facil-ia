// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
import {
  DOCUMENT_EVENTS_RETRY,
  DocumentEventsService,
  EVENT_SOURCE_FACTORY,
  EventSourceLike,
} from './document-events.service';
import { DocumentStatus } from './document.models';

class FakeEventSource implements EventSourceLike {
  readyState = 1;
  closed = false;
  onerror: ((event: Event) => void) | null = null;
  private readonly listeners: ((event: MessageEvent<string>) => void)[] = [];

  constructor(readonly url: string) {}

  addEventListener(type: 'status', listener: (event: MessageEvent<string>) => void): void {
    if (type === 'status') {
      this.listeners.push(listener);
    }
  }

  close(): void {
    this.closed = true;
    this.readyState = 2;
  }

  emit(data: string): void {
    this.listeners.forEach((listener) => listener(new MessageEvent('status', { data })));
  }

  fail(readyState: number): void {
    this.readyState = readyState;
    this.onerror?.(new Event('error'));
  }
}

describe('DocumentEventsService', () => {
  let sources: FakeEventSource[];
  let service: DocumentEventsService;

  beforeEach(() => {
    vi.useFakeTimers();
    sources = [];
    TestBed.configureTestingModule({
      providers: [
        {
          provide: EVENT_SOURCE_FACTORY,
          useValue: (url: string) => {
            const source = new FakeEventSource(url);
            sources.push(source);
            return source;
          },
        },
        { provide: DOCUMENT_EVENTS_RETRY, useValue: { maxRetries: 2, delayMs: 100 } },
      ],
    });
    service = TestBed.inject(DocumentEventsService);
  });

  afterEach(() => vi.useRealTimers());

  function watch(id = 'doc-1') {
    const statuses: DocumentStatus[] = [];
    const result: { completed: boolean; error: unknown } = { completed: false, error: null };
    const subscription = service.watch(id).subscribe({
      next: (status) => statuses.push(status),
      complete: () => (result.completed = true),
      error: (error: unknown) => (result.error = error),
    });
    return { statuses, result, subscription };
  }

  it('opens the events endpoint of the document', () => {
    watch('doc-1');

    expect(sources[0].url).toBe('/api/documents/doc-1/events');
  });

  it('emits each status and completes when the adaptation finishes', () => {
    const { statuses, result } = watch();

    sources[0].emit('{"id":"doc-1","status":"Received"}');
    sources[0].emit('{"id":"doc-1","status":"Processing"}');
    sources[0].emit('{"id":"doc-1","status":"Draft"}');

    expect(statuses).toEqual(['Received', 'Processing', 'Draft']);
    expect(result.completed).toBe(true);
    expect(sources[0].closed).toBe(true);
  });

  it('ignores malformed events and unknown statuses', () => {
    const { statuses } = watch();

    sources[0].emit('esto no es JSON');
    sources[0].emit('{"status":"Inventado"}');
    sources[0].emit('{"status":"Processing"}');

    expect(statuses).toEqual(['Processing']);
  });

  it('reopens the connection when the browser closes it for good', () => {
    const { statuses } = watch();

    sources[0].fail(2);
    vi.advanceTimersByTime(100);
    sources[1].emit('{"status":"Error"}');

    expect(sources.length).toBe(2);
    expect(statuses).toEqual(['Error']);
  });

  it('lets the browser reconnect on its own while it is still connecting', () => {
    watch();

    sources[0].fail(0);
    vi.advanceTimersByTime(1_000);

    expect(sources.length).toBe(1);
  });

  it('gives up with a user message after too many failures in a row', () => {
    const { result } = watch();

    sources[0].fail(0);
    sources[0].fail(0);
    sources[0].fail(0);

    expect(result.error).toBeInstanceOf(ApiError);
    expect((result.error as ApiError).message).toBe(ERROR_TEXTS.progressLost);
    expect(sources[0].closed).toBe(true);
  });

  it('closes the connection when the subscriber leaves', () => {
    const { subscription } = watch();

    subscription.unsubscribe();

    expect(sources[0].closed).toBe(true);
  });
});
