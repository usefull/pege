export class IcyParser {
    /**
     * @param {number} metaInterval Количество байтов аудиоданных между блоками метаданных (из заголовка icy-metaint)
     */
    constructor(metaInterval) {
        this.metaInterval = metaInterval || 0;
        this._resetState();
    }

    /**
     * Основной метод. Скармливаем ему сырой чанк данных из Response.body.getReader().
     * Возвращает массив событий: куски аудио и объекты с метаданными.
     * @param {Uint8Array} chunk 
     * @returns {{type: 'audio' | 'metadata', payload: Uint8Array | object}[]}
     */
    feed(chunk) {
        const events = [];
        let readPos = 0;

        while (readPos < chunk.length) {
            // Приоритет 1: Дочитываем метаданные, если процесс начат
            if (this.metadataLeft > 0) {
                const toCopy = Math.min(this.metadataLeft, chunk.length - readPos);
                const newMetaBuffer = new Uint8Array(this.metadataBuffer.length + toCopy);
                newMetaBuffer.set(this.metadataBuffer);
                newMetaBuffer.set(chunk.subarray(readPos, readPos + toCopy), this.metadataBuffer.length);
                this.metadataBuffer = newMetaBuffer;

                this.metadataLeft -= toCopy;
                readPos += toCopy;

                if (this.metadataLeft === 0) {
                    const strMetadata = new TextDecoder().decode(this.metadataBuffer).replace(/\0/g, '');
                    this.metadataBuffer = new Uint8Array(0);
                    events.push({ type: 'metadata', payload: this._parseIcyString(strMetadata) });
                }
                continue;
            }

            // Приоритет 2: Если включен режим ICY, ищем границу метаданных
            if (this.metaInterval > 0) {
                const distanceToMeta = this.metaInterval - this.bytesSinceAudio;
                const available = chunk.length - readPos;

                if (available < distanceToMeta) {
                    // В чанке только аудио
                    this._appendAudio(chunk.subarray(readPos));
                    readPos = chunk.length;
                } else {
                    // В чанке есть аудио, а сразу за ним — метаданные
                    if (distanceToMeta > 0) {
                        this._appendAudio(chunk.subarray(readPos, readPos + distanceToMeta));
                        readPos += distanceToMeta;
                    }
                    
                    // Читаем длину метаданных (следующий байт после аудио)
                    this.metadataLength = chunk[readPos] * 16;
                    readPos += 1;
                    this.bytesSinceAudio = 0; // Сбрасываем счетчик аудио

                    if (this.metadataLength === 0) {
                        // Пустой блок метаданных — полезно для сброса UI
                        events.push({ type: 'metadata', payload: {} });
                    } else {
                        this.metadataLeft = this.metadataLength;
                    }
                }
                continue;
            }

            // Приоритет 3: Режим без метаданных (metaInterval === 0)
            this._appendAudio(chunk.subarray(readPos));
            readPos = chunk.length;
        }

        return events;
    }

    /**
     * Забирает накопленный аудиобуфер. 
     * Вызывается после feed(), чтобы забрать аудио, которое накопилось внутри парсера.
     * @returns {Uint8Array | null}
     */
    takeAudio() {
        const buffer = this.audioBuffer;
        this.audioBuffer = new Uint8Array(0);
        return buffer.length > 0 ? buffer : null;
    }

    _appendAudio(data) {
        if (data.length === 0) return;
        const merged = new Uint8Array(this.audioBuffer.length + data.length);
        merged.set(this.audioBuffer);
        merged.set(data, this.audioBuffer.length);
        this.audioBuffer = merged;
        this.bytesSinceAudio += data.length;
    }

    _parseIcyString(str) {
        const data = {};
        const metadataParts = str.split(';');

        const parseField = (prefix) => {
            const field = metadataParts.find(s => s.startsWith(prefix));
            if (!field) return null;
            const start = field.indexOf("'") + 1;
            const end = field.lastIndexOf("'");
            return (start > 0 && end > start) ? field.substring(start, end).trim() : null;
        };

        const title = parseField("StreamTitle='");
        if (title) {
            const parts = title.split('-').map(s => s.trim());
            data.artist = parts[0] || null;
            data.track = parts[1] || null;
        } else {
            data.artist = null;
            data.track = null;
        }
        

        const next = parseField("NextTrack='");
        data.next = next ? `"${next}"` : null;

        const fromFlac = parseField("FromFlac='");
        data.fromFlac = fromFlac !== null ? fromFlac === '1' : undefined;

        return data;
    }

    _resetState() {
        this.audioBuffer = new Uint8Array(0);
        this.metadataBuffer = new Uint8Array(0);
        this.bytesSinceAudio = 0;
        this.metadataLeft = 0;
        this.metadataLength = 0;
    }

    /**
     * Вызывается при реконнекте к потоку, если меняется URL или заголовок icy-metaint
     * @param {number} newMetaInterval 
     */
    reset(newMetaInterval) {
        this._resetState();
        this.metaInterval = newMetaInterval || 0;
    }
}