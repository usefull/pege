import '../styles/toggle-switch.scss';

const ToggleSwitch = ({
  checked = false,
  onChange,
  label = '',
  labelPosition = 'right',
  disabled = false,
  className = '',
}) => {

  const handleChange = (event) => {
    if (onChange && typeof onChange === 'function') {
      onChange(event);
    }
  };

  return (
    <label className={`toggle-switch ${disabled ? 'disabled' : ''} ${className}`}
      style={{
        flexDirection: labelPosition === 'left' ? 'row-reverse' : 'row',
      }}
    >
      <input type="checkbox" checked={checked} onChange={handleChange}
        disabled={disabled}
        role="switch"
        aria-checked={checked}
        aria-disabled={disabled}
      />
      <span className={`slider${checked ? ' on' : ''}`}>
        <span className={`handle${checked ? ' on' : ''}`} />
      </span>
      {label && <span className="toggle-label">{label}</span>}
    </label>
  );
};

export default ToggleSwitch;