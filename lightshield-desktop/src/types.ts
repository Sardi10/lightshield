// src/types.ts
export interface Anomaly {
    id: number;
    timestamp: string;     // serialized ISO string from .NET
    type: string;          // e.g. "FailedLoginBurst"
    description: string;   // e.g. "7 failed logins in last 5 minutes"
    hostname: string;      // which machine/source triggered it
}
