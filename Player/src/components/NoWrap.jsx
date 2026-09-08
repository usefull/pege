import { useRef, useEffect, useState } from 'react';
import '../styles/no-wrap.scss';

const NoWrap = ({ children }) => {
    const containerRef = useRef(null);
    const innerRef = useRef(null);
    const [isHidden, setIsHidden] = useState(false);

    useEffect(() => {
        const container = containerRef.current;
        const inner = innerRef.current;
        if (!container || !inner) return;

        const checkOverflow = () => {
            const hasOverflow = inner.scrollWidth > container.clientWidth;
            setIsHidden(hasOverflow);
        };

        checkOverflow();

        const observer = new ResizeObserver(() => {
            window.requestAnimationFrame(checkOverflow);
        });
        observer.observe(container);

        return () => observer.disconnect();
    }, [children]);

    return (
        <div ref={containerRef} className={`no-wrap ${isHidden ? 'hidden' : ''}`}>
            <div ref={innerRef} className="no-wrap__inner">
                {children}
            </div>
        </div>
    );
};

export default NoWrap;