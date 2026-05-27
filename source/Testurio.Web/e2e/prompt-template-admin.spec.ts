import { test } from '@playwright/test';

// T030: E2E tests for the admin prompt template management UI are skipped.
// Feature 0047 is a backend-only feature (REST API + Cosmos + cache). There is no
// frontend UI surface for admin prompt template management in the current sprint.
// These tests will be implemented as part of a future frontend feature.

test.describe('Admin Prompt Template Management', () => {
  test.skip('GET /v1/admin/prompt-templates/{stage} - admin can view template', () => {
    // Placeholder — no UI exists yet.
  });

  test.skip('PUT /v1/admin/prompt-templates/{stage} - admin can update template body', () => {
    // Placeholder — no UI exists yet.
  });

  test.skip('Non-admin user is denied access to admin prompt template endpoints', () => {
    // Placeholder — no UI exists yet.
  });
});
