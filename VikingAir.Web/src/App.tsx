import { useState } from 'react';
import axios from 'axios';
import { motion, AnimatePresence } from 'framer-motion';
import { Shield, Database, Zap, Plane, CheckCircle, AlertCircle, RefreshCw, Sparkles } from 'lucide-react';

function App() {
  const [formData, setFormData] = useState({
    flightCode: '',
    passportNumber: '',
    seatPreference: 'Window'
  });
  const [result, setResult] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [tab, setTab] = useState<'booking' | 'architecture'>('booking');

  const handleBook = async () => {
    setLoading(true);
    setResult(null);
    try {
      const response = await axios.post('/api/book', formData);
      setResult({ success: true, data: response.data });
    } catch (err: any) {
      setResult({ success: false, error: err.response?.data || err.message });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-zinc-950 via-zinc-900 to-zinc-950 p-4 md:p-8">
      {/* Header */}
      <header className="max-w-7xl mx-auto mb-12">
        <div className="flex flex-col md:flex-row md:justify-between md:items-center gap-6">
          <motion.div
            initial={{ opacity: 0, y: -20 }}
            animate={{ opacity: 1, y: 0 }}
            className="flex items-center gap-4"
          >
            <div className="p-3 bg-gradient-to-br from-blue-600 to-blue-500 rounded-2xl glow">
              <Plane className="text-white" size={36} />
            </div>
            <div>
              <h1 className="text-4xl md:text-5xl font-bold tracking-tight text-white">
                Viking<span className="gradient-text">Air</span>
              </h1>
              <p className="text-zinc-400 text-sm mt-1">Powered by the Viking AOT Suite</p>
            </div>
          </motion.div>

          <nav className="glass p-2 flex gap-2">
            <button
              onClick={() => setTab('booking')}
              className={`tab-button ${tab === 'booking' ? 'tab-active' : 'tab-inactive'}`}
            >
              Booking Engine
            </button>
            <button
              onClick={() => setTab('architecture')}
              className={`tab-button ${tab === 'architecture' ? 'tab-active' : 'tab-inactive'}`}
            >
              Viking Suite
            </button>
          </nav>
        </div>
      </header>

      <main className="max-w-7xl mx-auto">
        {tab === 'booking' ? (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
            {/* Booking Form */}
            <motion.div
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              className="card space-y-6"
            >
              <div className="flex items-center gap-3 mb-6">
                <Sparkles className="text-blue-400" size={24} />
                <h2 className="text-2xl font-bold text-white">Flight Booking</h2>
              </div>

              <div className="space-y-5">
                <div>
                  <label className="block text-sm font-medium text-zinc-300 mb-2">
                    Flight Code
                  </label>
                  <input
                    className="input-field w-full"
                    value={formData.flightCode}
                    onChange={e => setFormData({ ...formData, flightCode: e.target.value })}
                    placeholder="e.g. VA123"
                  />
                  <p className="text-xs text-zinc-500 mt-1">Format: AA123 or AA1234</p>
                </div>

                <div>
                  <label className="block text-sm font-medium text-zinc-300 mb-2">
                    Passport Number
                  </label>
                  <input
                    className="input-field w-full"
                    value={formData.passportNumber}
                    onChange={e => setFormData({ ...formData, passportNumber: e.target.value })}
                    placeholder="ABC123DEF"
                  />
                  <p className="text-xs text-zinc-500 mt-1">5-20 characters</p>
                </div>

                <div>
                  <label className="block text-sm font-medium text-zinc-300 mb-2">
                    Seat Preference
                  </label>
                  <select
                    className="input-field w-full"
                    value={formData.seatPreference}
                    onChange={e => setFormData({ ...formData, seatPreference: e.target.value })}
                  >
                    <option value="Window">Window</option>
                    <option value="Aisle">Aisle</option>
                    <option value="Middle">Middle</option>
                  </select>
                </div>
              </div>

              <button
                onClick={handleBook}
                disabled={loading}
                className="btn-primary w-full flex items-center justify-center gap-3 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {loading ? (
                  <>
                    <RefreshCw className="animate-spin" size={20} />
                    Processing...
                  </>
                ) : (
                  <>
                    <Zap size={20} fill="currentColor" />
                    Confirm Booking
                  </>
                )}
              </button>
            </motion.div>

            {/* Result Panel */}
            <div className="space-y-6">
              <AnimatePresence mode="wait">
                {result ? (
                  <motion.div
                    key="result"
                    initial={{ opacity: 0, scale: 0.95 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.95 }}
                    className={`card border-t-4 ${result.success ? 'border-green-500' : 'border-red-500'}`}
                  >
                    <div className="flex items-center gap-4 mb-6">
                      {result.success ?
                        <CheckCircle className="text-green-500" size={40} /> :
                        <AlertCircle className="text-red-500" size={40} />
                      }
                      <div>
                        <h3 className="text-2xl font-bold text-white">
                          {result.success ? 'Booking Confirmed!' : 'Validation Failed'}
                        </h3>
                        <p className="text-zinc-400 text-sm">
                          {result.success ? 'Your flight has been reserved' : 'Please check your input'}
                        </p>
                      </div>
                    </div>

                    {result.success && result.data && (
                      <div className="space-y-4">
                        {/* Sannr Status Card */}
                        <div className="bg-gradient-to-r from-purple-500/10 to-indigo-500/10 border border-purple-500/20 rounded-xl p-4 flex items-center gap-4">
                          <div className="bg-purple-500/20 p-2 rounded-lg">
                            <Shield size={24} className="text-purple-400" />
                          </div>
                          <div>
                            <h4 className="font-semibold text-purple-200">Sannr Validation</h4>
                            <p className="text-sm text-purple-300/80">Zero-allocation validation & sanitization complete</p>
                          </div>
                          <CheckCircle size={20} className="text-purple-400 ml-auto" />
                        </div>

                        {/* Rapp Status Card */}
                        <div className="bg-gradient-to-r from-blue-500/10 to-cyan-500/10 border border-blue-500/20 rounded-xl p-4 flex items-center gap-4">
                          <div className="bg-blue-500/20 p-2 rounded-lg">
                            <Database size={24} className="text-blue-400" />
                          </div>
                          <div>
                            <h4 className="font-semibold text-blue-200">Rapp Caching</h4>
                            <p className="text-sm text-blue-300/80">Schema-safe binary caching active</p>
                          </div>
                          <CheckCircle size={20} className="text-blue-400 ml-auto" />
                        </div>

                        {/* Raw Response Data */}
                        <div className="bg-white/5 border border-white/10 rounded-xl p-5 mt-4">
                          <h4 className="text-xs font-semibold text-zinc-500 uppercase tracking-wider mb-3">
                            Server Response (Sanitized)
                          </h4>
                          <pre className="font-mono text-sm text-green-300 overflow-x-auto">
                            {JSON.stringify(result.data, null, 2)}
                          </pre>
                        </div>
                      </div>
                    )}

                    {!result.success && (
                      <div className="bg-gradient-to-br from-red-500/10 to-rose-500/10 border border-red-500/20 rounded-xl p-5 max-h-96 overflow-y-auto">
                        <pre className="text-sm text-red-300 font-mono whitespace-pre-wrap">
                          {typeof result.error === 'string' ? result.error : JSON.stringify(result.error, null, 2)}
                        </pre>
                      </div>
                    )}
                  </motion.div>
                ) : (
                  <motion.div
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    className="card text-center border-dashed border-2 border-zinc-800/50 py-16"
                  >
                    <Plane className="mx-auto text-zinc-700 mb-4" size={48} />
                    <p className="text-zinc-500">
                      Enter flight details to see the Viking Suite in action
                    </p>
                  </motion.div>
                )}
              </AnimatePresence>

              {/* Security Audit */}
              <div className="card bg-gradient-to-br from-purple-500/5 to-blue-500/5 border-purple-500/20">
                <h3 className="font-semibold mb-4 flex items-center gap-2 text-white">
                  <Shield size={20} className="text-purple-400" />
                  Viking Suite Status
                </h3>
                <ul className="space-y-3 text-sm">
                  <li className="flex items-start gap-3 text-zinc-300">
                    <CheckCircle size={18} className="text-purple-400 mt-0.5 flex-shrink-0" />
                    <span><span className="font-semibold text-purple-300">Sannr:</span> Zero-allocation validation via source generation</span>
                  </li>
                  <li className="flex items-start gap-3 text-zinc-300">
                    <CheckCircle size={18} className="text-blue-400 mt-0.5 flex-shrink-0" />
                    <span><span className="font-semibold text-blue-300">Rapp:</span> Schema-safe binary caching with hash verification</span>
                  </li>
                  <li className="flex items-start gap-3 text-zinc-300">
                    <CheckCircle size={18} className="text-green-400 mt-0.5 flex-shrink-0" />
                    <span><span className="font-semibold text-green-300">AOT:</span> Reflection-free execution for maximum performance</span>
                  </li>
                </ul>
              </div>
            </div>
          </div>
        ) : (
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="grid grid-cols-1 md:grid-cols-3 gap-6"
          >
            <div className="card border-t-4 border-green-500 hover:scale-105 transition-transform">
              <div className="p-4 bg-gradient-to-br from-green-500/20 to-emerald-500/20 rounded-2xl w-fit mb-6">
                <Shield className="text-green-400" size={32} />
              </div>
              <h3 className="text-xl font-bold mb-3 text-white">Sannr</h3>
              <p className="text-zinc-400 leading-relaxed">
                Zero-allocation validation engine. Moves reflection work to compile-time for 20x faster validation with 95% less memory.
              </p>
              <div className="mt-4 pt-4 border-t border-zinc-800">
                <span className="text-xs font-semibold text-green-400 uppercase tracking-wider">Source Generation</span>
              </div>
            </div>

            <div className="card border-t-4 border-blue-500 hover:scale-105 transition-transform">
              <div className="p-4 bg-gradient-to-br from-blue-500/20 to-cyan-500/20 rounded-2xl w-fit mb-6">
                <Database className="text-blue-400" size={32} />
              </div>
              <h3 className="text-xl font-bold mb-3 text-white">Rapp</h3>
              <p className="text-zinc-400 leading-relaxed">
                Schema-safe binary caching. Automatically invalidates cache if model structure changes, preventing Friday deployment crashes.
              </p>
              <div className="mt-4 pt-4 border-t border-zinc-800">
                <span className="text-xs font-semibold text-blue-400 uppercase tracking-wider">Binary Serialization</span>
              </div>
            </div>

            <div className="card border-t-4 border-purple-500 hover:scale-105 transition-transform">
              <div className="p-4 bg-gradient-to-br from-purple-500/20 to-pink-500/20 rounded-2xl w-fit mb-6">
                <Zap className="text-purple-400" size={32} />
              </div>
              <h3 className="text-xl font-bold mb-3 text-white">Skugga</h3>
              <p className="text-zinc-400 leading-relaxed">
                Native AOT mocking framework. Uses Roslyn Interceptors to mock dependencies without reflection, enabling true AOT testing.
              </p>
              <div className="mt-4 pt-4 border-t border-zinc-800">
                <span className="text-xs font-semibold text-purple-400 uppercase tracking-wider">Compile-time Mocking</span>
              </div>
            </div>
          </motion.div>
        )}
      </main>

      {/* Footer */}
      <footer className="max-w-7xl mx-auto mt-16 text-center text-zinc-600 text-sm">
        <p>Built with ❤️ using the Viking AOT Suite • Sannr • Rapp • Skugga • .NET 10</p>
      </footer>
    </div>
  );
}

export default App;
