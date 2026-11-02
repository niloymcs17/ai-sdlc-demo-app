import { test, expect, type APIRequestContext } from '@playwright/test';
import { InvoiceAppPage } from '../pages/InvoiceAppPage';
import { LoginPage } from '../pages/LoginPage';

test('Manager over-limit approval is refused and the invoice stays pending', async ({ page, request }) => {
  const invoiceId = await createPendingOverLimitInvoice(request);

  await page.goto('/');
  await new LoginPage(page).signIn('demo.manager', 'ChangeMe123!');

  const app = new InvoiceAppPage(page);
  await app.openApprovals();
  await app.approveInvoice(invoiceId);

  await expect(app.refusalAlert()).toHaveText(/exceeds the Manager approval limit/i);

  // A refusal does not re-fetch the lists. Reload so status and approvers come from the server.
  await page.reload();

  await expect(app.pendingApprovalCell(invoiceId)).toBeVisible();
  await expect(app.noApprovalsCell(invoiceId)).toBeVisible();

  await app.openApprovals();
  await expect(app.approveButton(invoiceId)).toBeVisible();
});

async function createPendingOverLimitInvoice(request: APIRequestContext): Promise<string> {
  const login = await request.post('/api/auth/login', {
    data: { username: 'demo.manager', password: 'ChangeMe123!' },
  });
  expect(login.status()).toBe(200);
  const { token } = (await login.json()) as { token: string; username: string; role: string };

  const created = await request.post('/api/invoices', {
    headers: { Authorization: `Bearer ${token}` },
    data: { customerName: `E2E over-limit ${Date.now()}`, amount: 75000 },
  });
  expect(created.status()).toBe(201);
  const { id } = (await created.json()) as { id: string };

  const submitted = await request.post(`/api/invoices/${id}/submit`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(submitted.ok()).toBeTruthy();

  return id;
}
