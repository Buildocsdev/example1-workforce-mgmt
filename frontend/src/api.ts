import axios from 'axios';

const api = axios.create({
  baseURL: 'http://localhost:7000',
  headers: {
    'Content-Type': 'application/json',
  },
});

export interface TestResponse {
  success: boolean;
  message: string;
}

export const runTest = async (): Promise<TestResponse> => {
  const response = await api.post<TestResponse>('/api/demo/test');
  return response.data;
};

export default api;