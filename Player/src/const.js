export const SERVER_ORIGIN = import.meta.env.VITE_SERVER_ORIGIN === 'DYNAMIC' ? window.location.origin : import.meta.env.VITE_SERVER_ORIGIN;
export const CENTRAL_FREQS = [62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
const ADTS_SAMPLE_RATES = [
    96000, 88200, 64000, 48000, 44100, 32000,
    24000, 22050, 16000, 12000, 11025, 8000, 7350
];
// Индексы битрейтов для MPEG-1 Layer III (в кбит/с). 
// Индекс 0 — free-формат (не поддерживается), индекс 15 — невалидный.
const MP3_BITRATES = [
    0,  32,  40,  48,  56,  64,  80,  96, 
    112, 128, 160, 192, 224, 256, 320, 0
];
// Частоты дискретизации для MPEG-1 (в Гц).
// Индекс 3 — зарезервирован/невалидный.
const MP3_SAMPLE_RATES = [
    44100, 48000, 32000, 0
];