/** @type {import('tailwindcss').Config} */
export default {
    content: [
        "./index.html",
        "./src/**/*.{js,ts,jsx,tsx}",
    ],
    theme: {
        extend: {
            colors: {
                primary: '#3b82f6',
                accent: '#a855f7',
            },
            fontFamily: {
                sans: ['Outfit', 'system-ui', 'sans-serif'],
            },
        },
    },
    plugins: [],
}
