export type ApiAuthMethod = 'none' | 'bearer' | 'api_key' | 'basic';
export type ApiAuthApiKeyPlacement = 'header' | 'query';

export interface ProjectApiAuthDto {
  projectId: string;
  apiAuthMethod: ApiAuthMethod;
  /** True when method is "bearer" and a token is stored. Null otherwise. */
  apiAuthBearerTokenConfigured: boolean | null;
  /** Pre-filled key name when method is "api_key". Null otherwise. */
  apiAuthApiKeyName: string | null;
  /** "header" | "query" when method is "api_key". Null otherwise. */
  apiAuthApiKeyPlacement: ApiAuthApiKeyPlacement | null;
  /** True when method is "api_key" and a value is stored. Null otherwise. */
  apiAuthApiKeyValueConfigured: boolean | null;
  /** Pre-filled username when method is "basic". Null otherwise. */
  apiAuthBasicUsername: string | null;
  /** True when method is "basic" and a password is stored. Null otherwise. */
  apiAuthBasicPasswordConfigured: boolean | null;
}

export interface UpdateProjectApiAuthRequest {
  apiAuthMethod: ApiAuthMethod;
  /** Required when method is "bearer". Never returned from API. */
  apiAuthBearerToken?: string;
  /** Required when method is "api_key". */
  apiAuthApiKeyName?: string;
  /** Required when method is "api_key". */
  apiAuthApiKeyPlacement?: ApiAuthApiKeyPlacement;
  /** Required when method is "api_key". Never returned from API. */
  apiAuthApiKeyValue?: string;
  /** Required when method is "basic". */
  apiAuthBasicUsername?: string;
  /** Required when method is "basic". Never returned from API. */
  apiAuthBasicPassword?: string;
}
