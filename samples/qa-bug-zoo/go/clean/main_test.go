package clean

import "testing"

func TestArithmetic(t *testing.T) {
	if 2+2 != 4 { t.Fatal("unexpected arithmetic result") }
}
