import { useState } from 'react'
import { useNavigate } from 'react-router-dom'

export default function Onboarding() {
    const [phone, setPhone] = useState('')
    const [email, setEmail] = useState('')
    const [error, setError] = useState<string | null>(null)
    const navigate = useNavigate()

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault()
        setError(null)
        try {
            const res = await fetch('http://localhost:5213/api/configuration', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ phoneNumber: phone, email }),
            })
            if (!res.ok) {
                const body = await res.json()
                throw new Error(body.error || 'Failed to save configuration')
            }
            navigate('/dashboard')
        } catch (err: unknown) {
               // Narrow down to Error to safely read .message
                    if (err instanceof Error) {
                         setError(err.message)
                            } else {
                      setError(String(err))
                        }
              }
    }

    return (
        <div className="p-8 max-w-md mx-auto">
            <h1 className="text-2xl font-semibold mb-4">Welcome to LightShield</h1>
            <form onSubmit={handleSubmit} className="space-y-4">
                <div>
                    <label className="block mb-1">Phone (E.164)</label>
                    <input
                        type="text"
                        value={phone}
                        onChange={e => setPhone(e.target.value)}
                        className="w-full border rounded p-2"
                        placeholder="+15551234567"
                        required
                    />
                </div>
                <div>
                    <label className="block mb-1">Email</label>
                    <input
                        type="email"
                        value={email}
                        onChange={e => setEmail(e.target.value)}
                        className="w-full border rounded p-2"
                        placeholder="alerts@company.com"
                        required
                    />
                </div>
                {error && <p className="text-red-600">{error}</p>}
                <button
                    type="submit"
                    className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
                >
                    Save & Continue
                </button>
            </form>
        </div>
    )
}
