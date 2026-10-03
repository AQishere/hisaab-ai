// Quantities are stored to three decimal places by the API. Compare integer
// thousandths instead of subtracting floating-point kilograms.
export const milli = (quantity: number) => Math.round(quantity * 1000);
export const quantityBalance = (input: number, output: number, waste: number) => (milli(input) - milli(output) - milli(waste)) / 1000;
export const wholeBags = (kg: number, bagSize: number) => milli(bagSize) > 0 && milli(kg) > 0 && milli(kg) % milli(bagSize) === 0;
