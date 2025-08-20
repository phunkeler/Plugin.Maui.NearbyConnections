# NearbyChat Design Specifications

## Component Library Structure

### 1. Design Tokens (`/components/design-tokens.ts`)
Centralized design system tokens for consistent styling across all components.

**Colors:**
- Primary: #2563eb (Blue 600) to #6d28d9 (Purple 700) gradient
- Success: #10b981 (Green 500)
- Avatar Colors: 6 predefined colors for user avatars
- Text: Primary (#111827), Secondary (#6b7280), Muted (#9ca3af)

**Typography:**
- Font sizes: xs (12px), sm (14px), base (16px), lg (18px), xl (20px), 2xl (24px), 3xl (30px)
- Font weights: normal (400), medium (500), semibold (600), bold (700)

**Spacing:**
- xs: 4px, sm: 8px, md: 12px, lg: 16px, xl: 24px, 2xl: 32px, 3xl: 48px, 4xl: 64px

**Border Radius:**
- sm: 4px, md: 6px, lg: 8px, xl: 12px, 2xl: 16px, full: 9999px

---

## Atomic Components

### 2. AppIcon Component (`/components/AppIcon.tsx`)
**Purpose:** Consistent app branding across different sizes
**Sizes:** sm (48px), md (64px), lg (80px)
**Features:**
- Gradient background (Blue to Purple)
- Users icon in center
- Optional WiFi connection indicator
- Consistent shadow and border radius

### 3. AvatarSelector Component (`/components/AvatarSelector.tsx`)
**Purpose:** User avatar selection with visual feedback
**Features:**
- 6 predefined avatar colors
- Letter-based fallbacks (A, B, C, D, E, F)
- Scale animation on selection
- Ring indicator for selected state

### 4. FormField Component (`/components/FormField.tsx`)
**Purpose:** Consistent form input styling
**Features:**
- Label with proper typography
- Styled input with focus states
- Consistent spacing and border radius

### 5. StatusIndicator Component (`/components/StatusIndicator.tsx`)
**Purpose:** Connection status display
**States:**
- Scanning: Green dot with pulse animation
- Connected: Static green dot
- Offline: Gray dot
**Typography:** xs (12px) with muted text color

---

## Composite Components

### 6. AppHeader Component (`/components/AppHeader.tsx`)
**Layout:**
- AppIcon (large size)
- App title (3xl, bold)
- Subtitle with highlighted plugin name
- Center-aligned text
- 48px bottom margin

### 7. LoginCard Component (`/components/LoginCard.tsx`)
**Structure:**
- Semi-transparent white background with blur
- Card header with title and subtitle
- Avatar selector section
- Form field for display name
- Gradient call-to-action button
- Consistent internal spacing (32px gaps)

### 8. AppFooter Component (`/components/AppFooter.tsx`)
**Elements:**
- "Powered by" text (sm, secondary color)
- Status indicator with text
- 48px top margin
- Center-aligned content

---

## Layout Structure

### 9. LoginPage Component (`/components/LoginPage.tsx`)
**Overall Layout:**
- Full viewport height
- Gradient background (Blue 50 to Indigo 100)
- Centered content container (max-width: 28rem)
- Vertical flex layout with proper spacing
- Mobile-first responsive design

---

## Visual Specifications

**Background Gradient:**
- From: #f8fafc (Blue 50)
- To: #e2e8f0 (Indigo 100)
- Direction: 135deg (bottom-right)

**Card Styling:**
- Background: rgba(255, 255, 255, 0.8)
- Backdrop filter: blur(8px)
- Shadow: 0 20px 25px -5px rgb(0 0 0 / 0.1)
- Border radius: 12px

**Button Gradient:**
- From: #2563eb (Blue 600)
- To: #6d28d9 (Purple 700)
- Hover states: Darker variants

**Animations:**
- Avatar selection: scale(1.1) with ring
- Status indicator: pulse animation for scanning
- Button hover: gradient shift
- All transitions: 200ms ease

---

## Responsive Behavior

**Mobile First:**
- Max width: 28rem (448px)
- Padding: 32px
- Full viewport height layout
- Touch-friendly button sizes (48px min height)
- Adequate spacing for touch targets

**Component Sizes:**
- Avatar selector: 48px per avatar
- Form inputs: 48px height minimum
- Buttons: 48px height minimum
- Icon sizes: proportional to container