import { useEffect, useRef } from 'react';
import AudioChain from './AudioChain';
import { IcyParser } from './IcyParser';
import { ADTS_SAMPLE_RATES, MP3_BITRATES, MP3_SAMPLE_RATES } from '../const';

import { useSetIsPlaying, useSetIsBuffering, useTogglePlayRequestId, useSetMetadata } from '../features/PlayerSlice';
import { useEqGrains, useEqOn } from '../features/EqualizerSlice';

import { CENTRAL_FREQS } from '../const';

const MIN_BUFFER_DURATION = 2.0;
const LOOK_AHEAD_TIME = 0.6;
const CONSUMER_TICK_MS = 50;
const INITIAL_RECONNECT_DELAY = 1000;
const MAX_RECONNECT_DELAY = 16000;
const FADE_DURATION = 0.4;
const MAX_QUEUE_DURATION = 10;

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

const extractMp3Frames = (buf) => {
    const frames = [];
    let offset = 0;

    while (offset + 4 <= buf.length) {
        // Сигнал синхронизации (Syncword): первые 11 бит установлены в 1 (0xFF + три старших бита)
        if (buf[offset] !== 0xFF || (buf[offset + 1] & 0xE0) !== 0xE0) {
            offset++;
            continue;
        }

        // Версия MPEG (биты 4-3 байта 1): 11 = MPEG Version 1 (самый частый для радио)
        const version = (buf[offset + 1] >> 3) & 0x03;
        if (version !== 3) { 
            offset++; // Пропускаем MPEG 2/2.5 для упрощения базовой формулы
            continue; 
        }

        // Слой звука (биты 2-1 байта 1): 01 = Layer III
        const layer = (buf[offset + 1] >> 1) & 0x03;
        if (layer !== 1) {
            offset++;
            continue;
        }

        const bitrateIdx = (buf[offset + 2] >> 4) & 0x0F;
        const sampleRateIdx = (buf[offset + 2] >> 2) & 0x03;
        const padding = (buf[offset + 2] >> 1) & 0x01;

        if (bitrateIdx === 0 || bitrateIdx === 15 || sampleRateIdx === 3) {
            offset++;
            continue;
        }

        const bitrate = MP3_BITRATES[bitrateIdx] * 1000;
        const sampleRate = MP3_SAMPLE_RATES[sampleRateIdx];

        // Формула длины фрейма MPEG-1 Layer III
        const frameLen = Math.floor((144 * bitrate) / sampleRate) + padding;

        if (frameLen < 4) { offset++; continue; }
        if (offset + frameLen > buf.length) break; // Ждем догрузки чанка

        frames.push(buf.subarray(offset, offset + frameLen));
        offset += frameLen;
    }

    return { frames, remaining: buf.subarray(offset) };
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
    const mp3ByteBufferRef = useRef(new Uint8Array(0));
    const isBufferingRef = useRef(true);
    const reconnectTimeoutRef = useRef(null);
    const reconnectDelayRef = useRef(INITIAL_RECONNECT_DELAY);
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

        if (reconnectDelayRef.current) {
            reconnectDelayRef.current = INITIAL_RECONNECT_DELAY;
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
            if (decoderRef.current.mp3 && typeof decoderRef.current.mp3.close === 'function') {
                try { decoderRef.current.mp3.close(); } catch {}
            }
            if (decoderRef.current.aac && typeof decoderRef.current.aac.close === 'function') {
                try { decoderRef.current.aac.close(); } catch {}
            }
            decoderRef.current = null;
        }
        decoderTypeRef.current = null;

        nextStartTimeRef.current = 0;
        audioQueueRef.current = [];
        aacByteBufferRef.current = new Uint8Array(0);
        mp3ByteBufferRef.current = new Uint8Array(0);
        
        setIsBuffering(false);
    };

    const connectAndReadStream = async () => {
        let currentDelay = reconnectDelayRef.current;

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

            let gotFirstChunk = false;

            while (isPlayingRef.current) {
                const { done, value } = await reader.read();

                if (done) {
                    if (!isPlayingRef.current) return;

                    // Сервер закрыл поток штатно. Уходим в буферизацию и реконнект.
                    isBufferingRef.current = true;
                    setIsBuffering(true);

                    console.warn(`Stream ended by server. Reconnecting in ${currentDelay}ms...`);

                    const nextDelay = Math.min(currentDelay * 2, MAX_RECONNECT_DELAY);
                    reconnectDelayRef.current = nextDelay;
                    reconnectTimeoutRef.current = setTimeout(
                        () => connectAndReadStream(),
                        currentDelay
                    );
                    return;
                }

                if (!gotFirstChunk) {
                    gotFirstChunk = true;

                    if (currentDelay !== INITIAL_RECONNECT_DELAY) {
                        currentDelay = INITIAL_RECONNECT_DELAY;
                        reconnectDelayRef.current = INITIAL_RECONNECT_DELAY;
                    }
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
            reconnectDelayRef.current = nextDelay;
            reconnectTimeoutRef.current = setTimeout(() => connectAndReadStream(), currentDelay);
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
            // Вспомогательная функция для оборачивания Uint8Array фрейма в чанк для WebCodecs
            const createAudioChunk = (frameBuffer) => new EncodedAudioChunk({
                type: 'key',
                timestamp: performance.now() * 1000, // Метка времени в микросекундах
                data: frameBuffer
            });

            // === ОБРАБОТКА MP3 ===
            if (decoderTypeRef.current === 'mp3' && decoderRef.current?.mp3) {
                const merged = new Uint8Array(mp3ByteBufferRef.current.length + value.length);
                merged.set(mp3ByteBufferRef.current, 0);
                merged.set(value, mp3ByteBufferRef.current.length);

                const { frames, remaining } = extractMp3Frames(merged);
                mp3ByteBufferRef.current = remaining;

                for (const frame of frames) {
                    decoderRef.current.mp3.decode(createAudioChunk(frame));
                }
                return true;
            }

            // === ОБРАБОТКА AAC ===
            if (decoderTypeRef.current === 'aac' && decoderRef.current?.aac) {
                const merged = new Uint8Array(aacByteBufferRef.current.length + value.length);
                merged.set(aacByteBufferRef.current, 0);
                merged.set(value, aacByteBufferRef.current.length);

                const { frames, remaining } = extractAdtsFrames(merged);
                aacByteBufferRef.current = remaining;

                for (const frame of frames) {
                    decoderRef.current.aac.decode(createAudioChunk(frame));
                }
                return true;
            }

            return false;
        } catch (decodeError) {
            console.warn('Ошибка в decodeAndEnqueue (WebCodecs):', decodeError);
            aacByteBufferRef.current = new Uint8Array(0);
            mp3ByteBufferRef.current = new Uint8Array(0);
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

    const createAACDescription = (sampleRate, channels) => {
        const sampleRates = [
            96000, 88200, 64000, 48000, 44100, 32000,
            24000, 22050, 16000, 12000, 11025, 8000, 7350
        ];
        
        const rateIndex = sampleRates.indexOf(sampleRate);
        // Если частота нестандартная, Chromium все равно может упасть, 
        // но для 44100 индекс будет 4, для 48000 — 3.
        const finalRateIndex = rateIndex !== -1 ? rateIndex : 4; 

        const config = new Uint8Array(2);
        // 5 бит: Audio Object Type (2 = AAC-LC)
        // 4 бита: Sample Rate Index (сдвигаем, так как индекс занимает часть первого и второго байта)
        config[0] = (2 << 3) | (finalRateIndex >> 1);
        // 4 бита: Channel Configuration
        config[1] = ((finalRateIndex & 1) << 7) | (channels << 3);
        
        return config;
    }

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

        // Функция-фабрика для WebCodecs декодера
        const createNativeDecoder = (codecName) => {
            return new AudioDecoder({
                output: (audioData) => {
                    const samples = audioData.numberOfFrames;
                    const channels = audioData.numberOfChannels;
                    
                    const leftData = new Float32Array(samples);
                    const rightData = channels > 1 ? new Float32Array(samples) : leftData;
                    
                    audioData.copyTo(leftData, { planeIndex: 0, format: 'f32-planar' });
                    if (channels > 1) {
                        audioData.copyTo(rightData, { planeIndex: 1, format: 'f32-planar' });
                    }

                    enqueueDecoded({
                        channelData: [leftData, rightData],
                        sampleRate: audioData.sampleRate
                    });
                    
                    audioData.close(); // КРИТИЧЕСКИ ВАЖНО для предотвращения утечек памяти!
                },
                error: (err) => console.error(`[AudioDecoder ${codecName}] Error:`, err)
            });
        };

        // Создаем нативные инстансы
        const aacDecoder = createNativeDecoder('AAC');
        const mp3Decoder = createNativeDecoder('MP3');

        // Конфигурируем их.
        aacDecoder.configure({
            codec: 'mp4a.40.02',
            sampleRate: probeResult.sampleRate || 44100,
            numberOfChannels: 2,
            description: createAACDescription(probeResult.sampleRate || 44100, 2) // обязательно для мобильных Opera, Firefox и Edge
        });
        mp3Decoder.configure({
            codec: 'mp3',
            sampleRate: 44100,
            numberOfChannels: 2
        });

        decoderRef.current = {
            mp3: mp3Decoder,
            aac: aacDecoder
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
        reconnectDelayRef.current = INITIAL_RECONNECT_DELAY;
        connectAndReadStream();
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