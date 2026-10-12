import '../styles/modal.scss';

const Modal = ({ visible, onClose, children }) => {
    return (
        <div className={`modal-overlay${visible ? ' active' : ''}`} onClick={() => { if (onClose) onClose(); }}>
            <div className="modal-window">{children}</div>
        </div>
    );
};

export default Modal;