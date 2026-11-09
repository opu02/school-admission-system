export const STUDENT_ID_LENGTH = 17;

export function isValidStudentId(value: string): boolean {
  return /^[0-9]{17}$/.test(value);
}

/** Message for the input box, or null when the value is valid. */
export function studentIdProblem(value: string): string | null {
  if (value.length === 0) return 'Please enter the 17-digit birth registration number.';
  if (!/^[0-9]*$/.test(value)) return 'Only numbers are allowed.';
  if (value.length !== STUDENT_ID_LENGTH) {
    return `It must be exactly 17 digits (you entered ${value.length}).`;
  }
  return null;
}
