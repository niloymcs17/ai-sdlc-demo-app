import { type Locator, type Page } from '@playwright/test';

export class InvoiceAppPage {
  constructor(private readonly page: Page) {}

  async openApprovals(): Promise<void> {
    await this.page.getByRole('tab', { name: 'Approvals' }).click();
  }

  async openInvoices(): Promise<void> {
    await this.page.getByRole('tab', { name: 'Invoices' }).click();
  }

  async approveInvoice(invoiceId: string): Promise<void> {
    await this.approveButton(invoiceId).click();
  }

  approveButton(invoiceId: string): Locator {
    return this.page.getByRole('button', { name: `Approve invoice ${invoiceId}` });
  }

  refusalAlert(): Locator {
    return this.page.getByRole('tabpanel', { name: 'Approvals' }).getByRole('alert');
  }

  pendingApprovalCell(invoiceId: string): Locator {
    return this.invoiceRow(invoiceId).getByRole('cell', { name: 'Pending Approval' });
  }

  noApprovalsCell(invoiceId: string): Locator {
    return this.invoiceRow(invoiceId).getByRole('cell', { name: 'None', exact: true });
  }

  private invoiceRow(invoiceId: string): Locator {
    return this.page
      .getByRole('table', { name: 'All invoices' })
      .getByRole('row', { name: new RegExp(invoiceId) });
  }
}
