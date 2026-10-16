import { useEffect, useRef } from 'react';
import { MPEGDecoder } from 'mpg123-decoder';
import { decoder as createAACDecoder } from '@audio/decode-aac';
import AudioChain from './AudioChain';
import { IcyParser } from './IcyParser';

import { useSetIsPlaying, useSetIsBuffering, useTogglePlayRequestId, useSetMetadata } from '../features/PlayerSlice';
import { useEqGrains, useEqOn } from '../features/EqualizerSlice';

import { CENTRAL_FREQS } from '../const';

const MIN_BUFFER_DURATION = 2.0;
const LOOK_AHEAD_TIME = 0.6;
const CONSUMER_TICK_MS = 50;
const INITIAL_RECONNECT_DELAY = 1000;
const MAX_RECONNECT_DELAY = 16000;
const FADE_DURATION = 0.4;
const MAX_QUEUE_DURATION = 30;

const ADTS_SAMPLE_RATES = [
    96000, 88200, 64000, 48000, 44100, 32000,
    24000, 22050, 16000, 12000, 11025, 8000, 7350
];

const STREAM_FETCH_HEADERS = {
    'Accept': 'audio/mpeg, audio/aac, audio/x-aac, audio/aacp',
    'Icy-MetaData': '1',
};

const detectStreamType = (response, url) => {
    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('aac') || contentType.includes('audio/aacp')) return 'aac';
    if (contentType.includes('mpeg') || contentType.includes('mp3')) return 'mp3';
    const lower = url.toLowerCase();
    return (lower.includes('aac') || lower.includes('aacp')) ? 'aac' : 'mp3';
};

const probeAacSampleRate = (buf) => {
    for (let i = 0; i + 7 <= buf.length; i++) {
        if (buf[i] !== 0xFF) continue;
        if ((buf[i + 1] & 0xF0) !== 0xF0) continue;

        // layer = биты 2-1 байта 1. У ADTS всегда 00, у MP3 — не 00.
        const layer = (buf[i + 1] >> 1) & 0x03;
        if (layer !== 0) continue;

        const freqIndex = (buf[i + 2] >> 2) & 0x0F;
        if (freqIndex > 12) continue;

        const sr = ADTS_SAMPLE_RATES[freqIndex];
        if (sr) return sr;
    }
    return 0;
};

const probeStream = async (url) => {
    const result = { type: null, sampleRate: 0 };
    const controller = new AbortController();

    try {
        const response = await fetch(url, {
            signal: controller.signal,
            headers: STREAM_FETCH_HEADERS,
        });

        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        if (!response.body) throw new Error('No body');

        result.type = detectStreamType(response, url);

        // sampleRate ищем только для AAC
        if (result.type === 'aac') {
            const reader = response.body.getReader();
            try {
                for (let attempt = 0; attempt < 5; attempt++) {
                    const { value, done } = await reader.read();
                    if (done || !value) break;
                    const sr = probeAacSampleRate(value);
                    if (sr) { result.sampleRate = sr; break; }
                }
            } finally {
                try { reader.cancel(); } catch { /* ignore */ }
            }
        }
    } catch (e) {
        console.warn('[Probe] failed:', e);
    } finally {
        try { controller.abort(); } catch { /* ignore */ }
    }

    return result;
};

const extractAdtsFrames = (buf) => {
    const frames = [];
    let offset = 0;

    while (offset + 7 <= buf.length) {
        // sync word: 0xFF 0xFx (старший ниббл второго байта = 0xF)
        if (buf[offset] !== 0xFF || (buf[offset + 1] & 0xF0) !== 0xF0) {
            offset++;
            continue;
        }

        // длина ADTS-фрейма: 13 бит, размазанных по байтам 3, 4, 5
        const frameLen =
            ((buf[offset + 3] & 0x03) << 11) |
             (buf[offset + 4] << 3) |
            ((buf[offset + 5] & 0xE0) >> 5);

        if (frameLen < 7) { offset++; continue; }        // битый фрейм
        if (offset + frameLen > buf.length) break;        // ждём ещё данных

        frames.push(buf.subarray(offset, offset + frameLen));
        offset += frameLen;
    }

    return { frames, remaining: buf.subarray(offset) };
};

const RadioPlayer = ({ streamUrl }) => {
    const togglePlayRequestId = useTogglePlayRequestId();
    const initialRequestIdRef = useRef(togglePlayRequestId);
    const setIsPlaying = useSetIsPlaying();
    const setIsBuffering = useSetIsBuffering();
    const setMetadata = useSetMetadata();
    const eqGrains = useEqGrains();
    const eqOn = useEqOn();

    const audioContextRef = useRef(null);
    const decoderRef = useRef(null);
    const decoderTypeRef = useRef(null); // 'mp3' или 'aac'
    const abortControllerRef = useRef(null);
    
    const isPlayingRef = useRef(false);
    const isStoppingRef = useRef(false);
    const nextStartTimeRef = useRef(0);
    const streamUrlRef = useRef(streamUrl);

    const equalizerOnRef = useRef(eqOn);
    const eqGrainsRef = useRef(eqGrains);

    const audioQueueRef = useRef([]);
    const aacByteBufferRef = useRef(new Uint8Array(0));
    const isBufferingRef = useRef(true);
    const reconnectTimeoutRef = useRef(null);
    const consumerTimerRef = useRef(null);

    const wakeLockRef = useRef(null);
    const visibilityHandlerRef = useRef(null);

    const audioChainRef = useRef(null);
    const isEqEnabledRef = useRef(true);

    const metadataRef = useRef(null);
    const streamInfoRef = useRef({});

    const icyParserRef = useRef(null);

    useEffect(() => {
        equalizerOnRef.current = eqOn;
        if (audioChainRef.current)
            audioChainRef.current.setEqualizerOn(eqOn);
    }, [eqOn]);

    useEffect(() => {
        eqGrainsRef.current = eqGrains;
        if (audioChainRef.current)
            audioChainRef.current.setEqGains(eqGrains);
    }, [eqGrains]);

    const requestWakeLock = async () => {
        if (!('wakeLock' in navigator)) return;
        try {
            if (wakeLockRef.current && !wakeLockRef.current.released) return;
            wakeLockRef.current = await navigator.wakeLock.request('screen');
            wakeLockRef.current.addEventListener('release', () => {
                if (isPlayingRef.current) {
                    setTimeout(() => { if (isPlayingRef.current) requestWakeLock(); }, 100);
                }
            });
        } catch (err) {
            console.warn(`Wake Lock failed: ${err.name}, ${err.message}`);
        }
    };

    const restoreWakeLockIfNeeded = async () => {
        if (isPlayingRef.current) {
            const hasValidLock = wakeLockRef.current && !wakeLockRef.current.released;
            if (!hasValidLock) await requestWakeLock();
        }
    };

    const releaseWakeLock = async () => {
        if (wakeLockRef.current) {
            try { await wakeLockRef.current.release(); } catch (e) { console.error(e); }
            wakeLockRef.current = null;
        }
    };

    // === Fade-out ===
    const stopPlaybackWithFade = () => {
        setIsPlaying(false);
        streamInfoRef.current = {};
        setMetadata({});
        return new Promise((resolve) => {
            if (!isPlayingRef.current || isStoppingRef.current) {
                resolve();
                return;
            }

            isStoppingRef.current = true;

            if (audioContextRef.current && audioChainRef.current) {
                const ctx = audioContextRef.current;
                const gain = audioChainRef.current.masterGain.gain;

                gain.cancelScheduledValues(ctx.currentTime);
                gain.setValueAtTime(gain.value, ctx.currentTime);
                gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + FADE_DURATION);
            }

            setTimeout(() => {
                forceStopAll();
                isStoppingRef.current = false;
                resolve();
            }, FADE_DURATION * 1000);
        });
    };

    // === Полная остановка ===
    const forceStopAll = () => {
        isStoppingRef.current = false;
        isPlayingRef.current = false;
        setIsPlaying(false);
        isBufferingRef.current = true;

        releaseWakeLock();

        if (visibilityHandlerRef.current) {
            document.removeEventListener('visibilitychange', visibilityHandlerRef.current);
            if (visibilityHandlerRef.current._resumeHandler) {
                document.removeEventListener('resume', visibilityHandlerRef.current._resumeHandler);
            }
            visibilityHandlerRef.current = null;
        }

        if (abortControllerRef.current) {
            abortControllerRef.current.abort();
            abortControllerRef.current = null;
        }

        if (reconnectTimeoutRef.current) {
            clearTimeout(reconnectTimeoutRef.current);
            reconnectTimeoutRef.current = null;
        }

        if (consumerTimerRef.current) {
            clearTimeout(consumerTimerRef.current);
            consumerTimerRef.current = null;
        }

        if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
            audioContextRef.current.close();
            audioContextRef.current = null;
        }

        if (audioChainRef.current) {
            try {
                audioChainRef.current.destroy();
            } catch (e) {
                console.warn('Error during AudioChain cleanup:', e);
            }
            audioChainRef.current = null;
            isEqEnabledRef.current = true;
        }

        if (decoderRef.current) {
            if (decoderRef.current.mp3 && typeof decoderRef.current.mp3.free === 'function') {
                decoderRef.current.mp3.free();
            }
            if (decoderRef.current.aac && typeof decoderRef.current.aac.free === 'function') {
                decoderRef.current.aac.free();
            } else if (decoderRef.current.aac && typeof decoderRef.current.aac.destroy === 'function') {
                decoderRef.current.aac.destroy();
            }
            decoderRef.current = null;
        }
        decoderTypeRef.current = null;

        nextStartTimeRef.current = 0;
        audioQueueRef.current = [];
        aacByteBufferRef.current = new Uint8Array(0);
        
        setIsBuffering(false);
    };

    const connectAndReadStream = async (currentDelay) => {
        if (!isPlayingRef.current) return;
        const currentUrl = streamUrlRef.current;

        // Отменяем предыдущий контроллер, если он ещё висит
        if (abortControllerRef.current) {
            try { abortControllerRef.current.abort(); } catch { /* ignore */ }
        }
        abortControllerRef.current = new AbortController();

        try {
            const response = await fetch(currentUrl, {
                signal: abortControllerRef.current.signal,
                headers: STREAM_FETCH_HEADERS,
            });

            if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
            if (!response.body) throw new Error('Empty stream body');

            decoderTypeRef.current = detectStreamType(response, currentUrl);

            let metaInterval = parseInt(response.headers.get('icy-metaint') || '', 10);
            metaInterval = isNaN(metaInterval) ? 0 : metaInterval;

            if (icyParserRef.current) {
                icyParserRef.current.reset(metaInterval);
            } else {
                icyParserRef.current = new IcyParser(metaInterval);
            }

            const icyNameHeader = response.headers.get('icy-name') || '';
            const parts = decodeURIComponent(icyNameHeader).split('|||');
            streamInfoRef.current = { ... streamInfoRef.current,
                name: parts.length > 0 ? parts[0].trim() : null,
                country: parts.length > 1 ? parts[1].trim() : null
            };
            setMetadata(streamInfoRef.current);

            const reader = response.body.getReader();

            while (isPlayingRef.current) {
                const { done, value } = await reader.read();

                if (done) {
                    if (!isPlayingRef.current) return;

                    // Сервер закрыл поток штатно. Уходим в буферизацию и реконнект.
                    isBufferingRef.current = true;
                    setIsBuffering(true);

                    console.warn(`Stream ended by server. Reconnecting in ${currentDelay}ms...`);

                    const nextDelay = Math.min(currentDelay * 2, MAX_RECONNECT_DELAY);
                    reconnectTimeoutRef.current = setTimeout(
                        () => connectAndReadStream(nextDelay),
                        currentDelay
                    );
                    return;
                }

                const events = icyParserRef.current.feed(value);

                for (const event of events) {
                    if (event.type === 'audio') {
                        const audioChunk = icyParserRef.current.takeAudio();
                        if (audioChunk && !(await decodeAndEnqueue(audioChunk))) {
                            continue;
                        }
                    }
                    
                    if (event.type === 'metadata') {
                        streamInfoRef.current = { ...streamInfoRef.current, ...event.payload };
                        setMetadata(streamInfoRef.current);
                    }
                }
                const pendingAudio = icyParserRef.current.takeAudio();
                if (pendingAudio && !(await decodeAndEnqueue(pendingAudio))) {
                    continue;
                }
            }
        } catch (error) {
            if (error.name === 'AbortError') return;
            console.warn(`Stream network error. Reconnecting in ${currentDelay}ms...`, error);
            if (abortControllerRef.current) abortControllerRef.current.abort();

            const nextDelay = Math.min(currentDelay * 2, MAX_RECONNECT_DELAY);
            reconnectTimeoutRef.current = setTimeout(() => connectAndReadStream(nextDelay), currentDelay);
        }
        finally {
            metadataRef.current = null;
        }
    };

    const enqueueDecoded = (decoded) => {
        if (!decoded || !decoded.channelData || decoded.channelData.length === 0) return false;

        const sampleRate = decoded.sampleRate;
        let leftData, rightData;

        if (Array.isArray(decoded.channelData)) {
            leftData = decoded.channelData[0];
            rightData = decoded.channelData.length > 1 ? decoded.channelData[1] : decoded.channelData[0];
        } else {
            leftData = decoded.channelData;
            rightData = decoded.channelData;
        }

        const samplesCount = leftData.length;
        if (samplesCount === 0) return false;

        audioQueueRef.current.push({
            leftData,
            rightData,
            samplesCount,
            sampleRate,
            duration: samplesCount / sampleRate,
        });

        // Ограничиваем суммарную длительность очереди.
        // Дропаем самые старые чанки, сохраняя самые свежие.
        let totalDuration = 0;
        let cutIndex = -1;

        for (let i = audioQueueRef.current.length - 1; i >= 0; i--) {
            totalDuration += audioQueueRef.current[i].duration;
            if (totalDuration > MAX_QUEUE_DURATION) {
                cutIndex = i;
                break;
            }
        }

        if (cutIndex > 0) {
            audioQueueRef.current.splice(0, cutIndex);
            console.warn(
                `Очередь превысила ${MAX_QUEUE_DURATION}s — дропнуто ${cutIndex} старых чанков`
            );
        }

        return true;
    };

    const decodeAndEnqueue = async (value) => {
        try {
            if (decoderTypeRef.current === 'mp3' && decoderRef.current?.mp3) {
                const decoded = decoderRef.current.mp3.decode(value);
                if (!decoded || decoded.samplesDecoded === 0 || !decoded.channelData) {
                    return true; // повреждённый чанк MP3 — пропускаем
                }
                enqueueDecoded(decoded);
                return true;
            }

            if (decoderTypeRef.current === 'aac' && decoderRef.current?.aac) {
                const merged = new Uint8Array(aacByteBufferRef.current.length + value.length);
                merged.set(aacByteBufferRef.current, 0);
                merged.set(value, aacByteBufferRef.current.length);

                const { frames, remaining } = extractAdtsFrames(merged);
                aacByteBufferRef.current = remaining;

                for (const frame of frames) {
                    const decodedFrame = await decoderRef.current.aac.decode(frame);
                    const hasData = !!(decodedFrame?.channelData?.[0]?.length);
                    if (!hasData) continue;
                    enqueueDecoded(decodedFrame);
                }
                return true;
            }

            return false;
        } catch (decodeError) {
            console.warn('Ошибка в decodeAndEnqueue:', decodeError);
            aacByteBufferRef.current = new Uint8Array(0);
            return false;
        }
    };

    // === Consumer ===
    const runConsumer = () => {
        if (!isPlayingRef.current || !audioContextRef.current) return;

        const context = audioContextRef.current;
        const totalQueueDuration = audioQueueRef.current.reduce((sum, chunk) => sum + chunk.duration, 0);

        if (isBufferingRef.current) {
            if (totalQueueDuration >= MIN_BUFFER_DURATION) {
                isBufferingRef.current = false;
                setIsBuffering(false);
                nextStartTimeRef.current = context.currentTime + 0.05;

                if (audioChainRef.current) {
                    const gain = audioChainRef.current.masterGain.gain;
                    gain.cancelScheduledValues(context.currentTime);
                    gain.setValueAtTime(0.001, context.currentTime);
                    gain.exponentialRampToValueAtTime(1.0, context.currentTime + FADE_DURATION);
                }
            } else {
                consumerTimerRef.current = setTimeout(runConsumer, CONSUMER_TICK_MS);
                return;
            }
        }

        if (audioQueueRef.current.length === 0 && nextStartTimeRef.current < context.currentTime) {
            isBufferingRef.current = true;
            setIsBuffering(true);
            
            if (audioChainRef.current) {
                audioChainRef.current.masterGain.gain.setValueAtTime(0.001, context.currentTime);
            }
            consumerTimerRef.current = setTimeout(runConsumer, CONSUMER_TICK_MS);
            return;
        }

        while (
            audioQueueRef.current.length > 0 && 
            (nextStartTimeRef.current - context.currentTime) < LOOK_AHEAD_TIME
        ) {
            const chunk = audioQueueRef.current.shift();

            const audioBuffer = context.createBuffer(2, chunk.samplesCount, chunk.sampleRate);
            audioBuffer.getChannelData(0).set(chunk.leftData);
            audioBuffer.getChannelData(1).set(chunk.rightData);

            const bufferSource = context.createBufferSource();
            bufferSource.buffer = audioBuffer;
            
            bufferSource.connect(audioChainRef.current.input);

            bufferSource.onended = () => {
                bufferSource.disconnect();
            };

            const scheduledTime = Math.max(nextStartTimeRef.current, context.currentTime);
            bufferSource.start(scheduledTime);
            nextStartTimeRef.current = scheduledTime + audioBuffer.duration;
        }

        consumerTimerRef.current = setTimeout(runConsumer, CONSUMER_TICK_MS);
    };

    const startPlayback = async () => {
        const currentUrl = streamUrlRef.current;
        if (!currentUrl) return;

        setIsBuffering(true);
        isBufferingRef.current = true;

        await requestWakeLock();

        const handleVisibilityChange = () => {
            if (document.visibilityState === 'visible' && isPlayingRef.current) restoreWakeLockIfNeeded();
        };

        if (visibilityHandlerRef.current) {
            document.removeEventListener('visibilitychange', visibilityHandlerRef.current);
            if (visibilityHandlerRef.current._resumeHandler) {
                document.removeEventListener('resume', visibilityHandlerRef.current._resumeHandler);
            }
        }

        const handleResume = () => { if (isPlayingRef.current) restoreWakeLockIfNeeded(); };
        document.addEventListener('resume', handleResume);
        handleVisibilityChange._resumeHandler = handleResume;
        visibilityHandlerRef.current = handleVisibilityChange;
        document.addEventListener('visibilitychange', handleVisibilityChange);

        const AudioContextClass = window.AudioContext || window.webkitAudioContext;

        // === Двойной коннект: probe отдельным запросом ===
        const probeResult = await probeStream(currentUrl);

        // Для MP3 частоту не задаём — контекст возьмёт дефолт устройства.
        const contextOptions = (probeResult.type === 'aac' && probeResult.sampleRate)
            ? { sampleRate: probeResult.sampleRate }
            : undefined;

        const context = new AudioContextClass(contextOptions);
        audioContextRef.current = context;

        // ПАРАЛЛЕЛЬНАЯ ИНИЦИАЛИЗАЦИЯ С ОБРАБОТКОЙ ОШИБОК
        const mp3Decoder = new MPEGDecoder();

        const mp3Ready = mp3Decoder.ready
            .then(() => mp3Decoder)
            .catch((err) => {
                console.warn('MP3 decoder init failed:', err);
                return null;
            });

        const aacReady = createAACDecoder().catch((err) => {
            console.warn('AAC decoder init failed:', err);
            return null;
        });

        const [mp3Instance, aacInstance] = await Promise.all([mp3Ready, aacReady]);

        // Если ни один декодер не поднялся — дальше идти некуда
        if (!mp3Instance && !aacInstance) {
            console.error('Оба декодера не инициализировались — воспроизведение невозможно');

            isPlayingRef.current = false;
            setIsPlaying(false);
            setIsBuffering(false);
            isBufferingRef.current = false;

            if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
                audioContextRef.current.close().catch(() => {});
                audioContextRef.current = null;
            }
            return;
        }

        decoderRef.current = {
            mp3: mp3Instance || null,
            aac: aacInstance || null
        };

        decoderTypeRef.current = null;

        audioChainRef.current = new AudioChain(
            context, 
            equalizerOnRef.current, 
            eqGrainsRef.current, 
            CENTRAL_FREQS
        );
        audioChainRef.current.masterGain.connect(context.destination);

        isPlayingRef.current = true;
        setIsPlaying(true);

        runConsumer();
        connectAndReadStream(INITIAL_RECONNECT_DELAY);
    };

    useEffect(() => {
        return () => {
            forceStopAll();
        };
    }, []);

    useEffect(() => {
        if (togglePlayRequestId === initialRequestIdRef.current) return;
        if (isPlayingRef.current)
            stopPlaybackWithFade();            
        else
            startPlayback();
    }, [togglePlayRequestId]);

    // Плавное переключение радиостанций
    useEffect(() => {
        let active = true;
        let timer = null;

        if (streamUrlRef.current !== streamUrl) setMetadata({});
        streamUrlRef.current = streamUrl;

        if (isPlayingRef.current) {
            stopPlaybackWithFade().then(() => {
                if (!active) return;
                timer = setTimeout(startPlayback, 50);
            });
        }

        return () => {
            active = false;
            if (timer) clearTimeout(timer);
        };
    }, [streamUrl]);

    return null;
};

export default RadioPlayer;