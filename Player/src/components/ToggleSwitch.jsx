import '../styles/toggle-switch.scss';

const ToggleSwitch = ({
  checked = false,
  onChange,
  disabled = false,
  className = '',
}) => {

  const handleChange = (event) => {
    if (onChange && typeof onChange === 'function') {
      onChange(event);
    }
  };

  return (
    <label className={`toggle-switch ${disabled ? 'disabled' : ''} ${className}`}>
      <input type="checkbox" checked={checked} onChange={handleChange}
        disabled={disabled}
        role="switch"
        aria-checked={checked}
        aria-disabled={disabled}
      />
      <span className="toggle-label off"></span>
      <span className={`slider${checked ? ' on' : ''}`}>
        <span className={`handle${checked ? ' on' : ''}`} />
      </span>
      <span className="toggle-label on"></span>
    </label>
  );
};

export default ToggleSwitch;