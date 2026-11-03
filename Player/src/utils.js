export const cleanString = (str) => {
    if (str === null || str === undefined) {
        return null;
    }

    const hasLetterOrDigit = /[\p{L}\p{N}]/u.test(str);
    
    return hasLetterOrDigit ? str : null;
};