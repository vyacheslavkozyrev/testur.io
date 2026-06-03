import type { NextConfig } from 'next';
import path from 'path';
import { config } from 'dotenv';

config({ path: path.resolve(__dirname, '../../.env'), override: false });

const nextConfig: NextConfig = {
  output: 'standalone',
};

export default nextConfig;
